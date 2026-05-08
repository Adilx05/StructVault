using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Persistence;

namespace StructVault.Persistence.Database;

public sealed class SqliteVaultNodeWriter : IVaultNodeWriter
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
}
