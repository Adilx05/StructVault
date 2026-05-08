using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Persistence;

namespace StructVault.Persistence.Database;

public sealed class SqliteVaultNodeWriter : IVaultNodeWriter, IVaultNodeReader
{
    public async Task CreateAsync(DbConnection connection, CreateVaultNodeCommand node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            INSERT INTO VaultNode (
                Id,
                ParentNodeId,
                Name,
                SortOrder,
                CreatedAtUtc,
                UpdatedAtUtc
            )
            VALUES (
                $id,
                $parentNodeId,
                $name,
                $sortOrder,
                $createdAtUtc,
                $updatedAtUtc
            );
            """;
        command.Parameters.AddWithValue("$id", node.Id);
        command.Parameters.AddWithValue("$parentNodeId", (object?)node.ParentNodeId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", node.Name);
        command.Parameters.AddWithValue("$sortOrder", node.SortOrder);
        command.Parameters.AddWithValue("$createdAtUtc", node.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$updatedAtUtc", node.UpdatedAtUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<VaultNodeRecord?> GetByIdAsync(DbConnection connection, GetVaultNodeByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            SELECT Id,
                   ParentNodeId,
                   Name,
                   SortOrder,
                   CreatedAtUtc,
                   UpdatedAtUtc
            FROM VaultNode
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", query.Id);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        VaultNodeRecord node = ReadNode(reader);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Vault node lookup returned multiple rows for a single node id.");
        }

        return node;
    }

    public async Task<IReadOnlyList<VaultNodeRecord>> ListAsync(DbConnection connection, ListVaultNodesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            SELECT Id,
                   ParentNodeId,
                   Name,
                   SortOrder,
                   CreatedAtUtc,
                   UpdatedAtUtc
            FROM VaultNode
            ORDER BY ParentNodeId IS NOT NULL, ParentNodeId, SortOrder, Name, Id;
            """;

        List<VaultNodeRecord> nodes = new();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            nodes.Add(ReadNode(reader));
        }

        return nodes;
    }

    public async Task<bool> UpdateAsync(DbConnection connection, UpdateVaultNodeCommand node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            UPDATE VaultNode
            SET Name = $name,
                SortOrder = $sortOrder,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", node.Id);
        command.Parameters.AddWithValue("$name", node.Name);
        command.Parameters.AddWithValue("$sortOrder", node.SortOrder);
        command.Parameters.AddWithValue("$updatedAtUtc", node.UpdatedAtUtc.ToString("O"));

        int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affectedRows > 1)
        {
            throw new InvalidOperationException("Vault node update affected multiple rows for a single node id.");
        }

        return affectedRows == 1;
    }

    public async Task DeleteAsync(DbConnection connection, DeleteVaultNodeCommand node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            DELETE FROM VaultNode
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", node.Id);

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
            throw new ArgumentException("Vault node persistence requires a SQLite connection.", nameof(connection));
        }

        if (sqliteConnection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Vault node persistence requires an open SQLite connection.");
        }

        return sqliteConnection;
    }

    private static VaultNodeRecord ReadNode(SqliteDataReader reader)
    {
        return new VaultNodeRecord(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            ParseTimestamp(reader.GetString(4), "CreatedAtUtc"),
            ParseTimestamp(reader.GetString(5), "UpdatedAtUtc"));
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

        throw new InvalidOperationException($"Vault node column '{columnName}' contains an invalid timestamp.");
    }
}
