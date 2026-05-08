using System.Data.Common;
using StructVault.Application.Persistence;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultOrderingPersistenceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc = new(2026, 5, 8, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task RootNodesAreOrderedBySortOrderThenNameThenId()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler nodeHandler = new(new SqliteVaultNodeWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await CreateNodeAsync(nodeHandler, connection, "zeta-root", null, "Zeta", 0);
        await CreateNodeAsync(nodeHandler, connection, "alpha-root", null, "Alpha", 0);
        await CreateNodeAsync(nodeHandler, connection, "beta-root-b", null, "Beta", 1);
        await CreateNodeAsync(nodeHandler, connection, "beta-root-a", null, "Beta", 1);
        await CreateNodeAsync(nodeHandler, connection, "first-root", null, "First", 2);

        IReadOnlyList<string> orderedNodeIds = await ReadOrderedIdsAsync(
            connection,
            """
            SELECT Id
            FROM VaultNode
            WHERE ParentNodeId IS NULL
            ORDER BY SortOrder, Name, Id;
            """);

        Assert.Equal(
            new[]
            {
                "alpha-root",
                "zeta-root",
                "beta-root-a",
                "beta-root-b",
                "first-root"
            },
            orderedNodeIds);
    }

    [Fact]
    public async Task ChildNodesAreOrderedWithinTheirParentBySortOrderThenNameThenId()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler nodeHandler = new(new SqliteVaultNodeWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await CreateNodeAsync(nodeHandler, connection, "parent-a", null, "Parent A", 0);
        await CreateNodeAsync(nodeHandler, connection, "parent-b", null, "Parent B", 1);
        await CreateNodeAsync(nodeHandler, connection, "parent-a-zeta", "parent-a", "Zeta", 0);
        await CreateNodeAsync(nodeHandler, connection, "parent-b-alpha", "parent-b", "Alpha", 0);
        await CreateNodeAsync(nodeHandler, connection, "parent-a-alpha", "parent-a", "Alpha", 0);
        await CreateNodeAsync(nodeHandler, connection, "parent-a-beta-b", "parent-a", "Beta", 1);
        await CreateNodeAsync(nodeHandler, connection, "parent-a-beta-a", "parent-a", "Beta", 1);

        IReadOnlyList<string> parentAChildIds = await ReadOrderedIdsAsync(
            connection,
            """
            SELECT Id
            FROM VaultNode
            WHERE ParentNodeId = 'parent-a'
            ORDER BY SortOrder, Name, Id;
            """);
        IReadOnlyList<string> parentBChildIds = await ReadOrderedIdsAsync(
            connection,
            """
            SELECT Id
            FROM VaultNode
            WHERE ParentNodeId = 'parent-b'
            ORDER BY SortOrder, Name, Id;
            """);

        Assert.Equal(
            new[]
            {
                "parent-a-alpha",
                "parent-a-zeta",
                "parent-a-beta-a",
                "parent-a-beta-b"
            },
            parentAChildIds);
        Assert.Equal(new[] { "parent-b-alpha" }, parentBChildIds);
    }

    [Fact]
    public async Task FieldsAreOrderedWithinTheirNodeBySortOrderThenId()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler nodeHandler = new(new SqliteVaultNodeWriter());
        CreateVaultFieldCommandHandler fieldHandler = new(new SqliteVaultFieldWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await CreateNodeAsync(nodeHandler, connection, "node-a", null, "Node A", 0);
        await CreateNodeAsync(nodeHandler, connection, "node-b", null, "Node B", 1);
        await CreateFieldAsync(fieldHandler, connection, "node-a-zeta", "node-a", "username", 0, new byte[] { 1 });
        await CreateFieldAsync(fieldHandler, connection, "node-b-alpha", "node-b", "username", 0, new byte[] { 2 });
        await CreateFieldAsync(fieldHandler, connection, "node-a-alpha", "node-a", "username", 0, new byte[] { 3 });
        await CreateFieldAsync(fieldHandler, connection, "node-a-second", "node-a", "username", 1, new byte[] { 4 });

        IReadOnlyList<string> nodeAFieldIds = await ReadOrderedIdsAsync(
            connection,
            """
            SELECT Id
            FROM VaultField
            WHERE NodeId = 'node-a'
            ORDER BY SortOrder, Id;
            """);
        IReadOnlyList<string> nodeBFieldIds = await ReadOrderedIdsAsync(
            connection,
            """
            SELECT Id
            FROM VaultField
            WHERE NodeId = 'node-b'
            ORDER BY SortOrder, Id;
            """);

        Assert.Equal(
            new[]
            {
                "node-a-alpha",
                "node-a-zeta",
                "node-a-second"
            },
            nodeAFieldIds);
        Assert.Equal(new[] { "node-b-alpha" }, nodeBFieldIds);
    }

    private static async Task CreateNodeAsync(
        CreateVaultNodeCommandHandler handler,
        DbConnection connection,
        string id,
        string? parentNodeId,
        string name,
        int sortOrder)
    {
        await handler.Handle(
            new CreateVaultNodeCommand(connection, id, parentNodeId, name, sortOrder, CreatedAtUtc, UpdatedAtUtc),
            CancellationToken.None);
    }

    private static async Task CreateFieldAsync(
        CreateVaultFieldCommandHandler handler,
        DbConnection connection,
        string id,
        string nodeId,
        string key,
        int sortOrder,
        byte[] value)
    {
        await handler.Handle(
            new CreateVaultFieldCommand(connection, id, nodeId, key, value, sortOrder, CreatedAtUtc, UpdatedAtUtc),
            CancellationToken.None);
    }

    private static async Task<IReadOnlyList<string>> ReadOrderedIdsAsync(DbConnection connection, string commandText)
    {
        List<string> ids = new();

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await using DbDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }
}
