using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Persistence;

namespace StructVault.Persistence.Database;

public sealed class SqliteVaultFieldWriter : IVaultFieldWriter
{
    public async Task CreateAsync(DbConnection connection, CreateVaultFieldCommand field, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(field);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            INSERT INTO VaultField (
                Id,
                NodeId,
                Key,
                Value,
                SortOrder,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES (
                $id,
                $nodeId,
                $key,
                $value,
                $sortOrder,
                $createdAtUtc,
                $updatedAtUtc
            );
            """;
        command.Parameters.AddWithValue("$id", field.Id);
        command.Parameters.AddWithValue("$nodeId", field.NodeId);
        command.Parameters.AddWithValue("$key", field.Key);
        command.Parameters.Add("$value", SqliteType.Blob).Value = field.Value;
        command.Parameters.AddWithValue("$sortOrder", field.SortOrder);
        command.Parameters.AddWithValue("$createdAtUtc", field.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$updatedAtUtc", field.UpdatedAtUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SqliteConnection RequireOpenSqliteConnection(DbConnection connection)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (connection is not SqliteConnection sqliteConnection)
        {
            throw new ArgumentException("Vault field persistence requires a SQLite connection.", nameof(connection));
        }

        if (sqliteConnection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Vault field persistence requires an open SQLite connection.");
        }

        return sqliteConnection;
    }
}
