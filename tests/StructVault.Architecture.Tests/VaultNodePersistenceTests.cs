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
