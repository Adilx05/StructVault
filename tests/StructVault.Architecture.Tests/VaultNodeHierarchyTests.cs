using System.Data.Common;
using StructVault.Application.Persistence;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultNodeHierarchyTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc = new(2026, 5, 8, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListVaultNodeHierarchyQueryHandlerBuildsNestedNodeStructureCorrectly()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultNodeWriter nodeStore = new();
        CreateVaultNodeCommandHandler createHandler = new(nodeStore);
        ListVaultNodeHierarchyQueryHandler hierarchyHandler = new(nodeStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await CreateNodeAsync(createHandler, connection, "root-b", null, "Beta Root", 1);
        await CreateNodeAsync(createHandler, connection, "root-a", null, "Alpha Root", 0);
        await CreateNodeAsync(createHandler, connection, "child-b", "root-a", "Beta Child", 1);
        await CreateNodeAsync(createHandler, connection, "child-a", "root-a", "Alpha Child", 0);
        await CreateNodeAsync(createHandler, connection, "grandchild", "child-a", "Grandchild", 0);

        IReadOnlyList<VaultNodeHierarchyRecord> hierarchy = await hierarchyHandler.Handle(
            new ListVaultNodeHierarchyQuery(connection),
            CancellationToken.None);

        Assert.Equal(new[] { "root-a", "root-b" }, hierarchy.Select(node => node.Id));
        VaultNodeHierarchyRecord rootA = hierarchy[0];
        Assert.Null(rootA.ParentNodeId);
        Assert.Equal("Alpha Root", rootA.Name);
        Assert.Equal(new[] { "child-a", "child-b" }, rootA.Children.Select(node => node.Id));
        Assert.Equal("root-a", rootA.Children[0].ParentNodeId);
        Assert.Equal(new[] { "grandchild" }, rootA.Children[0].Children.Select(node => node.Id));
        Assert.Equal("child-a", rootA.Children[0].Children[0].ParentNodeId);
        Assert.Empty(rootA.Children[1].Children);
        Assert.Empty(hierarchy[1].Children);
    }

    [Fact]
    public async Task ListVaultNodeHierarchyQueryHandlerReturnsEmptyHierarchyForEmptyVault()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        ListVaultNodeHierarchyQueryHandler hierarchyHandler = new(new SqliteVaultNodeWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);

        IReadOnlyList<VaultNodeHierarchyRecord> hierarchy = await hierarchyHandler.Handle(
            new ListVaultNodeHierarchyQuery(connection),
            CancellationToken.None);

        Assert.Empty(hierarchy);
    }

    [Fact]
    public async Task ListVaultNodeHierarchyQueryHandlerOrdersSiblingsBySortOrderThenNameThenId()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultNodeWriter nodeStore = new();
        CreateVaultNodeCommandHandler createHandler = new(nodeStore);
        ListVaultNodeHierarchyQueryHandler hierarchyHandler = new(nodeStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await CreateNodeAsync(createHandler, connection, "root", null, "Root", 0);
        await CreateNodeAsync(createHandler, connection, "zeta", "root", "Zeta", 0);
        await CreateNodeAsync(createHandler, connection, "beta-b", "root", "Beta", 1);
        await CreateNodeAsync(createHandler, connection, "alpha", "root", "Alpha", 0);
        await CreateNodeAsync(createHandler, connection, "beta-a", "root", "Beta", 1);

        IReadOnlyList<VaultNodeHierarchyRecord> hierarchy = await hierarchyHandler.Handle(
            new ListVaultNodeHierarchyQuery(connection),
            CancellationToken.None);

        Assert.Single(hierarchy);
        Assert.Equal(new[] { "alpha", "zeta", "beta-a", "beta-b" }, hierarchy[0].Children.Select(node => node.Id));
    }

    [Fact]
    public async Task ListVaultNodeHierarchyQueryHandlerRejectsOrphanedNode()
    {
        ListVaultNodeHierarchyQueryHandler hierarchyHandler = new(new StubVaultNodeReader(new[]
        {
            CreateRecord("orphan", "missing-parent", "Orphan", 0)
        }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => hierarchyHandler.Handle(
            new ListVaultNodeHierarchyQuery(new UnusedDbConnection()),
            CancellationToken.None));

        Assert.Contains("missing parent node 'missing-parent'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListVaultNodeHierarchyQueryHandlerRejectsCyclicNodeStructure()
    {
        ListVaultNodeHierarchyQueryHandler hierarchyHandler = new(new StubVaultNodeReader(new[]
        {
            CreateRecord("node-a", "node-b", "Node A", 0),
            CreateRecord("node-b", "node-a", "Node B", 0)
        }));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => hierarchyHandler.Handle(
            new ListVaultNodeHierarchyQuery(new UnusedDbConnection()),
            CancellationToken.None));

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VaultNodeHierarchyRecordRejectsChildAssignedToDifferentParent()
    {
        VaultNodeHierarchyRecord child = new(
            "child",
            "other-parent",
            "Child",
            0,
            CreatedAtUtc,
            UpdatedAtUtc,
            Array.Empty<VaultNodeHierarchyRecord>());

        Assert.Throws<ArgumentException>(() => new VaultNodeHierarchyRecord(
            "parent",
            null,
            "Parent",
            0,
            CreatedAtUtc,
            UpdatedAtUtc,
            new[] { child }));
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

    private static VaultNodeRecord CreateRecord(string id, string? parentNodeId, string name, int sortOrder)
    {
        return new VaultNodeRecord(id, parentNodeId, name, sortOrder, CreatedAtUtc, UpdatedAtUtc);
    }

    private sealed class StubVaultNodeReader : StructVault.Application.Abstractions.Persistence.IVaultNodeReader
    {
        private readonly IReadOnlyList<VaultNodeRecord> nodes;

        public StubVaultNodeReader(IReadOnlyList<VaultNodeRecord> nodes)
        {
            this.nodes = nodes;
        }

        public Task<VaultNodeRecord?> GetByIdAsync(
            DbConnection connection,
            GetVaultNodeByIdQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(nodes.SingleOrDefault(node => string.Equals(node.Id, query.Id, StringComparison.Ordinal)));
        }

        public Task<IReadOnlyList<VaultNodeRecord>> ListAsync(
            DbConnection connection,
            ListVaultNodesQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(nodes);
        }

        public Task<IReadOnlyList<VaultSearchResultRecord>> SearchAsync(
            DbConnection connection,
            SearchVaultQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<VaultSearchResultRecord>>(Array.Empty<VaultSearchResultRecord>());
        }
    }

    private sealed class UnusedDbConnection : DbConnection
    {
        public override string? ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override System.Data.ConnectionState State => System.Data.ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException();
        }

        public override void Close()
        {
        }

        public override void Open()
        {
            throw new NotSupportedException();
        }

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel)
        {
            throw new NotSupportedException();
        }

        protected override DbCommand CreateDbCommand()
        {
            throw new NotSupportedException();
        }
    }
}
