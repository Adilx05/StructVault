using System.Data;
using System.Data.Common;
using StructVault.Application.Persistence;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultFieldOrderingLogicTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 5, 8, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReorderedAtUtc = new(2026, 5, 8, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReorderVaultFieldCommandHandlerMovesFieldWithinNodeAndNormalizesSortOrders()
    {
        SqliteVaultFieldWriter fieldStore = new();
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler nodeHandler = new(new SqliteVaultNodeWriter());
        CreateVaultFieldCommandHandler createFieldHandler = new(fieldStore);
        ReorderVaultFieldCommandHandler reorderHandler = new(fieldStore);
        ListVaultFieldsByNodeIdQueryHandler listFieldsHandler = new(fieldStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await CreateNodeAsync(nodeHandler, connection, "root-node", "Root", 0);
        await CreateFieldAsync(createFieldHandler, connection, "field-a", "root-node", 10, new byte[] { 1 });
        await CreateFieldAsync(createFieldHandler, connection, "field-b", "root-node", 10, new byte[] { 2 });
        await CreateFieldAsync(createFieldHandler, connection, "field-c", "root-node", 50, new byte[] { 3 });

        bool reordered = await reorderHandler.Handle(
            new ReorderVaultFieldCommand(connection, " field-c ", 1, ReorderedAtUtc),
            CancellationToken.None);

        IReadOnlyList<VaultFieldRecord> fields = await listFieldsHandler.Handle(
            new ListVaultFieldsByNodeIdQuery(connection, "root-node"),
            CancellationToken.None);

        Assert.True(reordered);
        Assert.Equal(new[] { "field-a", "field-c", "field-b" }, fields.Select(field => field.Id));
        Assert.Equal(new[] { 0, 1, 2 }, fields.Select(field => field.SortOrder));
        Assert.All(fields, field => Assert.Equal(ReorderedAtUtc, field.UpdatedAtUtc));
    }

    [Fact]
    public async Task ReorderVaultFieldCommandHandlerClampsTargetSortOrderToEndAndDoesNotAffectOtherNodes()
    {
        SqliteVaultFieldWriter fieldStore = new();
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler nodeHandler = new(new SqliteVaultNodeWriter());
        CreateVaultFieldCommandHandler createFieldHandler = new(fieldStore);
        ReorderVaultFieldCommandHandler reorderHandler = new(fieldStore);
        ListVaultFieldsByNodeIdQueryHandler listFieldsHandler = new(fieldStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await CreateNodeAsync(nodeHandler, connection, "root-node", "Root", 0);
        await CreateNodeAsync(nodeHandler, connection, "other-node", "Other", 1);
        await CreateFieldAsync(createFieldHandler, connection, "field-a", "root-node", 0, new byte[] { 1 });
        await CreateFieldAsync(createFieldHandler, connection, "field-b", "root-node", 1, new byte[] { 2 });
        await CreateFieldAsync(createFieldHandler, connection, "field-c", "root-node", 2, new byte[] { 3 });
        await CreateFieldAsync(createFieldHandler, connection, "other-field-a", "other-node", 25, new byte[] { 4 });
        await CreateFieldAsync(createFieldHandler, connection, "other-field-b", "other-node", 26, new byte[] { 5 });

        bool reordered = await reorderHandler.Handle(
            new ReorderVaultFieldCommand(connection, "field-a", 99, ReorderedAtUtc),
            CancellationToken.None);

        IReadOnlyList<VaultFieldRecord> rootFields = await listFieldsHandler.Handle(
            new ListVaultFieldsByNodeIdQuery(connection, "root-node"),
            CancellationToken.None);
        IReadOnlyList<VaultFieldRecord> otherFields = await listFieldsHandler.Handle(
            new ListVaultFieldsByNodeIdQuery(connection, "other-node"),
            CancellationToken.None);

        Assert.True(reordered);
        Assert.Equal(new[] { "field-b", "field-c", "field-a" }, rootFields.Select(field => field.Id));
        Assert.Equal(new[] { 0, 1, 2 }, rootFields.Select(field => field.SortOrder));
        Assert.Equal(new[] { "other-field-a", "other-field-b" }, otherFields.Select(field => field.Id));
        Assert.Equal(new[] { 25, 26 }, otherFields.Select(field => field.SortOrder));
        Assert.All(otherFields, field => Assert.Equal(CreatedAtUtc, field.UpdatedAtUtc));
    }

    [Fact]
    public async Task ReorderVaultFieldCommandHandlerReturnsFalseForMissingField()
    {
        SqliteVaultFieldWriter fieldStore = new();
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        ReorderVaultFieldCommandHandler reorderHandler = new(fieldStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);

        bool reordered = await reorderHandler.Handle(
            new ReorderVaultFieldCommand(connection, "missing-field", 0, ReorderedAtUtc),
            CancellationToken.None);

        Assert.False(reordered);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReorderVaultFieldCommandRejectsMissingFieldId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ReorderVaultFieldCommand(
            new UnusedDbConnection(),
            id!,
            0,
            ReorderedAtUtc));
    }

    [Fact]
    public void ReorderVaultFieldCommandRejectsNegativeTargetSortOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReorderVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            -1,
            ReorderedAtUtc));
    }

    [Fact]
    public void ReorderVaultFieldCommandRejectsDefaultUpdatedTimestamp()
    {
        Assert.Throws<ArgumentException>(() => new ReorderVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            0,
            default));
    }

    private static async Task CreateNodeAsync(
        CreateVaultNodeCommandHandler handler,
        DbConnection connection,
        string id,
        string name,
        int sortOrder)
    {
        await handler.Handle(
            new CreateVaultNodeCommand(connection, id, null, name, sortOrder, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
    }

    private static async Task CreateFieldAsync(
        CreateVaultFieldCommandHandler handler,
        DbConnection connection,
        string id,
        string nodeId,
        int sortOrder,
        byte[] value)
    {
        await handler.Handle(
            new CreateVaultFieldCommand(connection, id, nodeId, "field-key", value, sortOrder, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
    }

    private sealed class UnusedDbConnection : DbConnection
    {
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException();
        }

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotSupportedException();
        }

        protected override DbCommand CreateDbCommand()
        {
            throw new NotSupportedException();
        }
    }
}
