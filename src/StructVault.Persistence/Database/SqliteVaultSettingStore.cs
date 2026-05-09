using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Persistence;

namespace StructVault.Persistence.Database;

public sealed class SqliteVaultSettingStore : IVaultSettingReader, IVaultSettingWriter
{
    public async Task<IReadOnlyList<VaultSettingRecord>> ListByNamesAsync(
        DbConnection connection,
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        cancellationToken.ThrowIfCancellationRequested();

        string[] normalizedNames = names.Select(RequireSupportedSettingName).Distinct(StringComparer.Ordinal).ToArray();
        if (normalizedNames.Length == 0)
        {
            return Array.Empty<VaultSettingRecord>();
        }

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);
        await using SqliteCommand command = sqliteConnection.CreateCommand();
        string[] parameterNames = Enumerable.Range(0, normalizedNames.Length).Select(index => "$name" + index).ToArray();
        command.CommandText = $"""
            SELECT Name, Value
            FROM VaultSetting
            WHERE Name IN ({string.Join(", ", parameterNames)})
            ORDER BY Name;
            """;

        for (int index = 0; index < normalizedNames.Length; index++)
        {
            command.Parameters.AddWithValue(parameterNames[index], normalizedNames[index]);
        }

        List<VaultSettingRecord> settings = new();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            settings.Add(new VaultSettingRecord(reader.GetString(0), reader.GetString(1)));
        }

        return settings;
    }

    public async Task UpsertManyAsync(
        DbConnection connection,
        IReadOnlyCollection<VaultSettingRecord> settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        VaultSettingRecord[] normalizedSettings = settings
            .Select(setting => new VaultSettingRecord(
                RequireSupportedSettingName(setting.Name),
                RequireNonEmpty(setting.Value, nameof(setting.Value))))
            .ToArray();
        if (normalizedSettings.Length == 0)
        {
            return;
        }

        if (normalizedSettings.Select(setting => setting.Name).Distinct(StringComparer.Ordinal).Count() != normalizedSettings.Length)
        {
            throw new ArgumentException("Vault setting names cannot be duplicated within one save operation.", nameof(settings));
        }

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);
        await using SqliteTransaction transaction = (SqliteTransaction)await sqliteConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        bool transactionCompleted = false;
        try
        {
            foreach (VaultSettingRecord setting in normalizedSettings)
            {
                await UpsertAsync(sqliteConnection, transaction, setting, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            transactionCompleted = true;
        }
        catch
        {
            if (!transactionCompleted)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }

    private static async Task UpsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        VaultSettingRecord setting,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO VaultSetting (Name, Value, UpdatedAtUtc)
            VALUES ($name, $value, $updatedAtUtc)
            ON CONFLICT(Name) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$name", setting.Name);
        command.Parameters.AddWithValue("$value", setting.Value);
        command.Parameters.AddWithValue("$updatedAtUtc", DateTimeOffset.UtcNow.ToString("O"));

        int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affectedRows != 1)
        {
            throw new InvalidOperationException("Vault setting save did not affect exactly one row.");
        }
    }

    private static SqliteConnection RequireOpenSqliteConnection(DbConnection connection)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (connection is not SqliteConnection sqliteConnection)
        {
            throw new ArgumentException("Vault settings require a SQLite connection.", nameof(connection));
        }

        if (sqliteConnection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Vault settings require an open SQLite connection.");
        }

        return sqliteConnection;
    }

    private static string RequireSupportedSettingName(string value)
    {
        string normalizedValue = RequireNonEmpty(value, "settingName");
        if (!VaultSettingNames.IsSupported(normalizedValue))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Vault setting name is not supported.");
        }

        return normalizedValue;
    }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Value cannot be empty or whitespace.", parameterName);
        }

        return normalizedValue;
    }
}
