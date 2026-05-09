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


    public async Task<IReadOnlyList<VaultSearchResultRecord>> SearchAsync(DbConnection connection, SearchVaultQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteCommand command = sqliteConnection.CreateCommand();
        command.CommandText = """
            SELECT Id,
                   Name
            FROM VaultNode
            WHERE Name COLLATE NOCASE LIKE $pattern ESCAPE '\'
            ORDER BY ParentNodeId IS NOT NULL, ParentNodeId, SortOrder, Name, Id;
            """;
        command.Parameters.AddWithValue("$pattern", CreateLikePattern(query.SearchText));

        List<VaultSearchResultRecord> results = new();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new VaultSearchResultRecord(
                VaultSearchResultKind.Node,
                reader.GetString(0),
                reader.GetString(1),
                null,
                null,
                "Node name"));
        }

        return results;
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


    public async Task<bool> ReorderAsync(DbConnection connection, ReorderVaultNodeCommand node, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(node);
        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection sqliteConnection = RequireOpenSqliteConnection(connection);

        await using SqliteTransaction transaction = (SqliteTransaction)await sqliteConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        bool transactionCompleted = false;
        try
        {
            string? parentNodeId = await ReadNodeParentIdAsync(sqliteConnection, transaction, node.Id, cancellationToken).ConfigureAwait(false);
            if (parentNodeId is null && !await NodeExistsAsync(sqliteConnection, transaction, node.Id, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                transactionCompleted = true;
                return false;
            }

            List<string> orderedNodeIds = await ReadOrderedSiblingNodeIdsAsync(sqliteConnection, transaction, parentNodeId, cancellationToken).ConfigureAwait(false);
            if (!orderedNodeIds.Remove(node.Id))
            {
                throw new InvalidOperationException("Vault node reordering could not locate the node within its sibling set.");
            }

            int targetIndex = Math.Min(node.TargetSortOrder, orderedNodeIds.Count);
            orderedNodeIds.Insert(targetIndex, node.Id);

            for (int sortOrder = 0; sortOrder < orderedNodeIds.Count; sortOrder++)
            {
                await UpdateNodeSortOrderAsync(
                    sqliteConnection,
                    transaction,
                    orderedNodeIds[sortOrder],
                    node.Id,
                    sortOrder,
                    node.UpdatedAtUtc,
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



    private static async Task<bool> NodeExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string nodeId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(1)
            FROM VaultNode
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", nodeId);

        object? count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return count is long value && value == 1;
    }

    private static async Task<string?> ReadNodeParentIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string nodeId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT ParentNodeId
            FROM VaultNode
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", nodeId);

        object? parentNodeId = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return parentNodeId is null || parentNodeId is DBNull ? null : (string)parentNodeId;
    }

    private static async Task<List<string>> ReadOrderedSiblingNodeIdsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string? parentNodeId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        if (parentNodeId is null)
        {
            command.CommandText = """
                SELECT Id
                FROM VaultNode
                WHERE ParentNodeId IS NULL
                ORDER BY SortOrder, Name, Id;
                """;
        }
        else
        {
            command.CommandText = """
                SELECT Id
                FROM VaultNode
                WHERE ParentNodeId = $parentNodeId
                ORDER BY SortOrder, Name, Id;
                """;
            command.Parameters.AddWithValue("$parentNodeId", parentNodeId);
        }

        List<string> nodeIds = new();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            nodeIds.Add(reader.GetString(0));
        }

        return nodeIds;
    }

    private static async Task UpdateNodeSortOrderAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string nodeId,
        string movedNodeId,
        int sortOrder,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE VaultNode
            SET SortOrder = $sortOrder,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id
              AND (SortOrder <> $sortOrder OR Id = $movedNodeId);
            """;
        command.Parameters.AddWithValue("$id", nodeId);
        command.Parameters.AddWithValue("$movedNodeId", movedNodeId);
        command.Parameters.AddWithValue("$sortOrder", sortOrder);
        command.Parameters.AddWithValue("$updatedAtUtc", updatedAtUtc.ToString("O"));

        int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affectedRows > 1)
        {
            throw new InvalidOperationException("Vault node reordering updated multiple rows for a single node id.");
        }
    }

    private static string CreateLikePattern(string searchText)
    {
        return "%" + EscapeLikePattern(searchText.Trim()) + "%";
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
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
