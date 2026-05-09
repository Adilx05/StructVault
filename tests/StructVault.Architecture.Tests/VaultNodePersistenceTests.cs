using System.Data.Common;
using StructVault.Application.Persistence;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultNodePersistenceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc = new(2026, 5, 8, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateVaultNodeCommandHandlerStoresNodeCorrectly()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler handler = new(new SqliteVaultNodeWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        CreateVaultNodeCommand command = new(
            connection,
            " root-node ",
            null,
            " Personal Vault ",
            10,
            CreatedAtUtc,
            UpdatedAtUtc);

        await handler.Handle(command, CancellationToken.None);

        Assert.Equal(1L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM VaultNode WHERE Id = 'root-node';"));
        Assert.Equal(DBNull.Value, await ExecuteScalarAsync(connection, "SELECT ParentNodeId FROM VaultNode WHERE Id = 'root-node';"));
        Assert.Equal("Personal Vault", await ExecuteScalarAsync(connection, "SELECT Name FROM VaultNode WHERE Id = 'root-node';"));
        Assert.Equal(10L, await ExecuteScalarAsync(connection, "SELECT SortOrder FROM VaultNode WHERE Id = 'root-node';"));
        Assert.Equal(CreatedAtUtc.ToString("O"), await ExecuteScalarAsync(connection, "SELECT CreatedAtUtc FROM VaultNode WHERE Id = 'root-node';"));
        Assert.Equal(UpdatedAtUtc.ToString("O"), await ExecuteScalarAsync(connection, "SELECT UpdatedAtUtc FROM VaultNode WHERE Id = 'root-node';"));
    }

    [Fact]
    public async Task CreateVaultNodeCommandHandlerStoresChildNodeCorrectly()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler handler = new(new SqliteVaultNodeWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await handler.Handle(
            new CreateVaultNodeCommand(connection, "root", null, "Root", 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);

        await handler.Handle(
            new CreateVaultNodeCommand(connection, "child", " root ", "Child", 1, CreatedAtUtc, UpdatedAtUtc),
            CancellationToken.None);

        Assert.Equal("root", await ExecuteScalarAsync(connection, "SELECT ParentNodeId FROM VaultNode WHERE Id = 'child';"));
        Assert.Equal("Child", await ExecuteScalarAsync(connection, "SELECT Name FROM VaultNode WHERE Id = 'child';"));
    }

    [Fact]
    public async Task DeleteVaultNodeCommandHandlerCascadesDeleteToFieldsForDeletedNodeOnly()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultNodeWriter nodeWriter = new();
        CreateVaultNodeCommandHandler createNodeHandler = new(nodeWriter);
        DeleteVaultNodeCommandHandler deleteNodeHandler = new(nodeWriter);
        CreateVaultFieldCommandHandler createFieldHandler = new(new SqliteVaultFieldWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await createNodeHandler.Handle(
            new CreateVaultNodeCommand(connection, "deleted-node", null, "Deleted", 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createNodeHandler.Handle(
            new CreateVaultNodeCommand(connection, "remaining-node", null, "Remaining", 1, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createFieldHandler.Handle(
            new CreateVaultFieldCommand(connection, "deleted-field-1", "deleted-node", "username", new byte[] { 1 }, 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createFieldHandler.Handle(
            new CreateVaultFieldCommand(connection, "deleted-field-2", "deleted-node", "password", new byte[] { 2 }, 1, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createFieldHandler.Handle(
            new CreateVaultFieldCommand(connection, "remaining-field", "remaining-node", "username", new byte[] { 3 }, 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);

        await deleteNodeHandler.Handle(new DeleteVaultNodeCommand(connection, " deleted-node "), CancellationToken.None);

        Assert.Equal(0L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM VaultNode WHERE Id = 'deleted-node';"));
        Assert.Equal(0L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM VaultField WHERE NodeId = 'deleted-node';"));
        Assert.Equal(1L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM VaultNode WHERE Id = 'remaining-node';"));
        Assert.Equal(1L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM VaultField WHERE Id = 'remaining-field';"));
    }


    [Fact]
    public async Task GetVaultNodeByIdQueryHandlerReturnsStoredNode()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultNodeWriter nodeStore = new();
        CreateVaultNodeCommandHandler createHandler = new(nodeStore);
        GetVaultNodeByIdQueryHandler getHandler = new(nodeStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await createHandler.Handle(
            new CreateVaultNodeCommand(connection, "root", null, "Root", 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createHandler.Handle(
            new CreateVaultNodeCommand(connection, " child ", " root ", " Child ", 1, CreatedAtUtc, UpdatedAtUtc),
            CancellationToken.None);

        VaultNodeRecord? node = await getHandler.Handle(new GetVaultNodeByIdQuery(connection, " child "), CancellationToken.None);

        Assert.NotNull(node);
        Assert.Equal("child", node.Id);
        Assert.Equal("root", node.ParentNodeId);
        Assert.Equal("Child", node.Name);
        Assert.Equal(1, node.SortOrder);
        Assert.Equal(CreatedAtUtc, node.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, node.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetVaultNodeByIdQueryHandlerReturnsNullForMissingNode()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        GetVaultNodeByIdQueryHandler getHandler = new(new SqliteVaultNodeWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);

        VaultNodeRecord? node = await getHandler.Handle(new GetVaultNodeByIdQuery(connection, "missing-node"), CancellationToken.None);

        Assert.Null(node);
    }

    [Fact]
    public async Task ListVaultNodesQueryHandlerReturnsNodesInStablePersistenceOrder()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultNodeWriter nodeStore = new();
        CreateVaultNodeCommandHandler createHandler = new(nodeStore);
        ListVaultNodesQueryHandler listHandler = new(nodeStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await createHandler.Handle(
            new CreateVaultNodeCommand(connection, "root-b", null, "Beta", 1, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createHandler.Handle(
            new CreateVaultNodeCommand(connection, "root-a", null, "Alpha", 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createHandler.Handle(
            new CreateVaultNodeCommand(connection, "child-b", "root-a", "Beta Child", 1, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createHandler.Handle(
            new CreateVaultNodeCommand(connection, "child-a", "root-a", "Alpha Child", 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);

        IReadOnlyList<VaultNodeRecord> nodes = await listHandler.Handle(new ListVaultNodesQuery(connection), CancellationToken.None);

        Assert.Equal(new[] { "root-a", "root-b", "child-a", "child-b" }, nodes.Select(node => node.Id));
    }

    [Fact]
    public async Task UpdateVaultNodeCommandHandlerRenamesAndReordersExistingNode()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultNodeWriter nodeStore = new();
        CreateVaultNodeCommandHandler createHandler = new(nodeStore);
        UpdateVaultNodeCommandHandler updateHandler = new(nodeStore);
        GetVaultNodeByIdQueryHandler getHandler = new(nodeStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await createHandler.Handle(
            new CreateVaultNodeCommand(connection, "root", null, "Root", 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);

        bool updated = await updateHandler.Handle(
            new UpdateVaultNodeCommand(connection, " root ", " Renamed Root ", 5, UpdatedAtUtc),
            CancellationToken.None);
        VaultNodeRecord? node = await getHandler.Handle(new GetVaultNodeByIdQuery(connection, "root"), CancellationToken.None);

        Assert.True(updated);
        Assert.NotNull(node);
        Assert.Equal("Renamed Root", node.Name);
        Assert.Equal(5, node.SortOrder);
        Assert.Equal(CreatedAtUtc, node.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, node.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateVaultNodeCommandHandlerRenamesNodeWithoutChangingIdentityHierarchyOrFields()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultNodeWriter nodeStore = new();
        SqliteVaultFieldWriter fieldStore = new();
        CreateVaultNodeCommandHandler createNodeHandler = new(nodeStore);
        UpdateVaultNodeCommandHandler updateNodeHandler = new(nodeStore);
        GetVaultNodeByIdQueryHandler getNodeHandler = new(nodeStore);
        CreateVaultFieldCommandHandler createFieldHandler = new(fieldStore);
        GetVaultFieldByIdQueryHandler getFieldHandler = new(fieldStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await createNodeHandler.Handle(
            new CreateVaultNodeCommand(connection, "root", null, "Root", 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createNodeHandler.Handle(
            new CreateVaultNodeCommand(connection, "child", "root", "Child", 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
        await createFieldHandler.Handle(
            new CreateVaultFieldCommand(connection, "root-field", "root", "username", new byte[] { 1, 2, 3 }, 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);

        bool updated = await updateNodeHandler.Handle(
            new UpdateVaultNodeCommand(connection, " root ", " Renamed Root ", 3, UpdatedAtUtc),
            CancellationToken.None);
        VaultNodeRecord? renamedRoot = await getNodeHandler.Handle(new GetVaultNodeByIdQuery(connection, "root"), CancellationToken.None);
        VaultNodeRecord? child = await getNodeHandler.Handle(new GetVaultNodeByIdQuery(connection, "child"), CancellationToken.None);
        VaultFieldRecord? linkedField = await getFieldHandler.Handle(new GetVaultFieldByIdQuery(connection, "root-field"), CancellationToken.None);

        Assert.True(updated);
        Assert.NotNull(renamedRoot);
        Assert.Equal("root", renamedRoot.Id);
        Assert.Null(renamedRoot.ParentNodeId);
        Assert.Equal("Renamed Root", renamedRoot.Name);
        Assert.Equal(3, renamedRoot.SortOrder);
        Assert.Equal(CreatedAtUtc, renamedRoot.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, renamedRoot.UpdatedAtUtc);

        Assert.NotNull(child);
        Assert.Equal("root", child.ParentNodeId);
        Assert.Equal("Child", child.Name);

        Assert.NotNull(linkedField);
        Assert.Equal("root", linkedField.NodeId);
        Assert.Equal("username", linkedField.Key);
    }

    [Fact]
    public async Task UpdateVaultNodeCommandHandlerReturnsFalseForMissingNode()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        UpdateVaultNodeCommandHandler updateHandler = new(new SqliteVaultNodeWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);

        bool updated = await updateHandler.Handle(
            new UpdateVaultNodeCommand(connection, "missing-node", "Missing", 0, UpdatedAtUtc),
            CancellationToken.None);

        Assert.False(updated);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateVaultNodeCommandRejectsMissingNodeId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CreateVaultNodeCommand(
            new UnusedDbConnection(),
            id!,
            null,
            "Root",
            0,
            CreatedAtUtc,
            UpdatedAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateVaultNodeCommandRejectsMissingNodeName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CreateVaultNodeCommand(
            new UnusedDbConnection(),
            "root",
            null,
            name!,
            0,
            CreatedAtUtc,
            UpdatedAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeleteVaultNodeCommandRejectsMissingNodeId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new DeleteVaultNodeCommand(
            new UnusedDbConnection(),
            id!));
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetVaultNodeByIdQueryRejectsMissingNodeId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new GetVaultNodeByIdQuery(
            new UnusedDbConnection(),
            id!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateVaultNodeCommandRejectsMissingNodeId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new UpdateVaultNodeCommand(
            new UnusedDbConnection(),
            id!,
            "Root",
            0,
            UpdatedAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateVaultNodeCommandRejectsMissingNodeName(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new UpdateVaultNodeCommand(
            new UnusedDbConnection(),
            "root",
            name!,
            0,
            UpdatedAtUtc));
    }

    [Fact]
    public void UpdateVaultNodeCommandRejectsNegativeSortOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UpdateVaultNodeCommand(
            new UnusedDbConnection(),
            "root",
            "Root",
            -1,
            UpdatedAtUtc));
    }

    [Fact]
    public void UpdateVaultNodeCommandRejectsDefaultUpdatedTimestamp()
    {
        Assert.Throws<ArgumentException>(() => new UpdateVaultNodeCommand(
            new UnusedDbConnection(),
            "root",
            "Root",
            0,
            default));
    }

    [Fact]
    public void CreateVaultNodeCommandRejectsSelfParent()
    {
        Assert.Throws<ArgumentException>(() => new CreateVaultNodeCommand(
            new UnusedDbConnection(),
            "root",
            " root ",
            "Root",
            0,
            CreatedAtUtc,
            UpdatedAtUtc));
    }

    [Fact]
    public void CreateVaultNodeCommandRejectsNegativeSortOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CreateVaultNodeCommand(
            new UnusedDbConnection(),
            "root",
            null,
            "Root",
            -1,
            CreatedAtUtc,
            UpdatedAtUtc));
    }

    [Fact]
    public void CreateVaultNodeCommandRejectsDefaultTimestamps()
    {
        Assert.Throws<ArgumentException>(() => new CreateVaultNodeCommand(
            new UnusedDbConnection(),
            "root",
            null,
            "Root",
            0,
            default,
            UpdatedAtUtc));

        Assert.Throws<ArgumentException>(() => new CreateVaultNodeCommand(
            new UnusedDbConnection(),
            "root",
            null,
            "Root",
            0,
            CreatedAtUtc,
            default));
    }

    [Fact]
    public void CreateVaultNodeCommandRejectsUpdatedTimestampBeforeCreatedTimestamp()
    {
        Assert.Throws<ArgumentException>(() => new CreateVaultNodeCommand(
            new UnusedDbConnection(),
            "root",
            null,
            "Root",
            0,
            UpdatedAtUtc,
            CreatedAtUtc));
    }

    private static async Task<object?> ExecuteScalarAsync(DbConnection connection, string commandText)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteScalarAsync();
    }

    private sealed class UnusedDbConnection : DbConnection
    {
        public override string ConnectionString { get; set; } = string.Empty;

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
