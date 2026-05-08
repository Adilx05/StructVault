using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Persistence;

namespace StructVault.Persistence.Database;

public sealed class SqliteVaultFieldWriter : IVaultFieldWriter, IVaultFieldReader
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

    public async Task<VaultFieldRecord?> GetByIdAsync(DbConnection connection, GetVaultFieldByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            SELECT Id,
                   NodeId,
                   Key,
                   Value,
                   SortOrder,
                   CreatedAtUtc,
                   UpdatedAtUtc
            FROM VaultField
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", query.Id);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        VaultFieldRecord field = ReadField(reader);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Vault field lookup returned multiple rows for a single field id.");
        }

        return field;
    }

    public async Task<IReadOnlyList<VaultFieldRecord>> ListByNodeIdAsync(DbConnection connection, ListVaultFieldsByNodeIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            SELECT Id,
                   NodeId,
                   Key,
                   Value,
                   SortOrder,
                   CreatedAtUtc,
                   UpdatedAtUtc
            FROM VaultField
            WHERE NodeId = $nodeId
            ORDER BY SortOrder, Id;
            """;
        command.Parameters.AddWithValue("$nodeId", query.NodeId);

        List<VaultFieldRecord> fields = new();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            fields.Add(ReadField(reader));
        }

        return fields;
    }

    public async Task<bool> UpdateAsync(DbConnection connection, UpdateVaultFieldCommand field, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(field);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            UPDATE VaultField
            SET Key = $key,
                Value = $value,
                SortOrder = $sortOrder,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", field.Id);
        command.Parameters.AddWithValue("$key", field.Key);
        command.Parameters.Add("$value", SqliteType.Blob).Value = field.Value;
        command.Parameters.AddWithValue("$sortOrder", field.SortOrder);
        command.Parameters.AddWithValue("$updatedAtUtc", field.UpdatedAtUtc.ToString("O"));

        int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affectedRows > 1)
        {
            throw new InvalidOperationException("Vault field update affected multiple rows for a single field id.");
        }

        return affectedRows == 1;
    }

    public async Task<bool> ReorderAsync(DbConnection connection, ReorderVaultFieldCommand field, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(field);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteTransaction transaction = (SqliteTransaction)await sqliteConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        bool transactionCompleted = false;
        try
        {
            string? nodeId = await ReadFieldNodeIdAsync(sqliteConnection, transaction, field.Id, cancellationToken).ConfigureAwait(false);
            if (nodeId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                transactionCompleted = true;
                return false;
            }

            List<string> orderedFieldIds = await ReadOrderedFieldIdsAsync(sqliteConnection, transaction, nodeId, cancellationToken).ConfigureAwait(false);
            if (!orderedFieldIds.Remove(field.Id))
            {
                throw new InvalidOperationException("Vault field reordering could not locate the field within its owning node.");
            }

            int targetIndex = Math.Min(field.TargetSortOrder, orderedFieldIds.Count);
            orderedFieldIds.Insert(targetIndex, field.Id);

            for (int sortOrder = 0; sortOrder < orderedFieldIds.Count; sortOrder++)
            {
                await UpdateFieldSortOrderAsync(
                    sqliteConnection,
                    transaction,
                    orderedFieldIds[sortOrder],
                    field.Id,
                    sortOrder,
                    field.UpdatedAtUtc,
                    cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            transactionCompleted = true;
            return true;
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

    public async Task DeleteAsync(DbConnection connection, DeleteVaultFieldCommand field, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(field);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            DELETE FROM VaultField
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", field.Id);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadFieldNodeIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string fieldId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT NodeId
            FROM VaultField
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", fieldId);

        object? nodeId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return nodeId is null || nodeId is DBNull ? null : (string)nodeId;
    }

    private static async Task<List<string>> ReadOrderedFieldIdsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string nodeId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id
            FROM VaultField
            WHERE NodeId = $nodeId
            ORDER BY SortOrder, Id;
            """;
        command.Parameters.AddWithValue("$nodeId", nodeId);

        List<string> fieldIds = new();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            fieldIds.Add(reader.GetString(0));
        }

        return fieldIds;
    }

    private static async Task UpdateFieldSortOrderAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string fieldId,
        string movedFieldId,
        int sortOrder,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE VaultField
            SET SortOrder = $sortOrder,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id
              AND (SortOrder <> $sortOrder OR Id = $movedFieldId);
            """;
        command.Parameters.AddWithValue("$id", fieldId);
        command.Parameters.AddWithValue("$movedFieldId", movedFieldId);
        command.Parameters.AddWithValue("$sortOrder", sortOrder);
        command.Parameters.AddWithValue("$updatedAtUtc", updatedAtUtc.ToString("O"));

        int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affectedRows > 1)
        {
            throw new InvalidOperationException("Vault field reordering updated multiple rows for a single field id.");
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
            throw new ArgumentException("Vault field persistence requires a SQLite connection.", nameof(connection));
        }

        if (sqliteConnection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Vault field persistence requires an open SQLite connection.");
        }

        return sqliteConnection;
    }

    private static VaultFieldRecord ReadField(SqliteDataReader reader)
    {
        return new VaultFieldRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            (byte[])reader[3],
            reader.GetInt32(4),
            ParseTimestamp(reader.GetString(5), "CreatedAtUtc"),
            ParseTimestamp(reader.GetString(6), "UpdatedAtUtc"));
    }

    private static DateTimeOffset ParseTimestamp(string value, string columnName)
    {
        if (DateTimeOffset.TryParseExact(
            value,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTimeOffset timestamp))
        {
            return timestamp.ToUniversalTime();
        }

        throw new InvalidOperationException($"Vault field column '{columnName}' contains an invalid timestamp.");
    }
}
