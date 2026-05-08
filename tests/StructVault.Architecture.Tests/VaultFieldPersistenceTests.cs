using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using StructVault.Application.Persistence;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultFieldPersistenceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 5, 8, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc = new(2026, 5, 8, 13, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateVaultFieldCommandHandlerStoresFieldLinkedToNode()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler nodeHandler = new(new SqliteVaultNodeWriter());
        CreateVaultFieldCommandHandler fieldHandler = new(new SqliteVaultFieldWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await nodeHandler.Handle(
            new CreateVaultNodeCommand(connection, " root-node ", null, "Root", 0, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);

        byte[] fieldValue = { 10, 20, 30, 40 };
        CreateVaultFieldCommand command = new(
            connection,
            " login-field ",
            " root-node ",
            " username ",
            fieldValue,
            7,
            CreatedAtUtc,
            UpdatedAtUtc);

        await fieldHandler.Handle(command, CancellationToken.None);

        Assert.Equal(1L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM VaultField WHERE Id = 'login-field';"));
        Assert.Equal("root-node", await ExecuteScalarAsync(connection, "SELECT NodeId FROM VaultField WHERE Id = 'login-field';"));
        Assert.Equal("Root", await ExecuteScalarAsync(
            connection,
            """
            SELECT VaultNode.Name
            FROM VaultField
            INNER JOIN VaultNode ON VaultNode.Id = VaultField.NodeId
            WHERE VaultField.Id = 'login-field';
            """));
        Assert.Equal("username", await ExecuteScalarAsync(connection, "SELECT Key FROM VaultField WHERE Id = 'login-field';"));
        Assert.Equal(fieldValue, await ExecuteBytesAsync(connection, "SELECT Value FROM VaultField WHERE Id = 'login-field';"));
        Assert.Equal(7L, await ExecuteScalarAsync(connection, "SELECT SortOrder FROM VaultField WHERE Id = 'login-field';"));
        Assert.Equal(CreatedAtUtc.ToString("O"), await ExecuteScalarAsync(connection, "SELECT CreatedAtUtc FROM VaultField WHERE Id = 'login-field';"));
        Assert.Equal(UpdatedAtUtc.ToString("O"), await ExecuteScalarAsync(connection, "SELECT UpdatedAtUtc FROM VaultField WHERE Id = 'login-field';"));
    }

    [Fact]
    public async Task CreateVaultFieldCommandHandlerRejectsMissingNodeLink()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultFieldCommandHandler fieldHandler = new(new SqliteVaultFieldWriter());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        CreateVaultFieldCommand command = new(
            connection,
            "field",
            "missing-node",
            "username",
            new byte[] { 1 },
            0,
            CreatedAtUtc,
            UpdatedAtUtc);

        await Assert.ThrowsAsync<SqliteException>(async () =>
            await fieldHandler.Handle(command, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateVaultFieldCommandRejectsMissingFieldId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CreateVaultFieldCommand(
            new UnusedDbConnection(),
            id!,
            "node",
            "username",
            new byte[] { 1 },
            0,
            CreatedAtUtc,
            UpdatedAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateVaultFieldCommandRejectsMissingNodeId(string? nodeId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CreateVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            nodeId!,
            "username",
            new byte[] { 1 },
            0,
            CreatedAtUtc,
            UpdatedAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateVaultFieldCommandRejectsMissingKey(string? key)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CreateVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            "node",
            key!,
            new byte[] { 1 },
            0,
            CreatedAtUtc,
            UpdatedAtUtc));
    }

    [Fact]
    public void CreateVaultFieldCommandRejectsMissingValue()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            "node",
            "username",
            null!,
            0,
            CreatedAtUtc,
            UpdatedAtUtc));

        Assert.Throws<ArgumentException>(() => new CreateVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            "node",
            "username",
            Array.Empty<byte>(),
            0,
            CreatedAtUtc,
            UpdatedAtUtc));
    }

    [Fact]
    public void CreateVaultFieldCommandCopiesValueBytes()
    {
        byte[] originalValue = { 1, 2, 3 };

        CreateVaultFieldCommand command = new(
            new UnusedDbConnection(),
            "field",
            "node",
            "username",
            originalValue,
            0,
            CreatedAtUtc,
            UpdatedAtUtc);

        originalValue[0] = 9;
        byte[] commandValue = command.Value;
        commandValue[1] = 8;

        Assert.Equal(new byte[] { 1, 2, 3 }, command.Value);
    }

    [Fact]
    public void CreateVaultFieldCommandRejectsNegativeSortOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CreateVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            "node",
            "username",
            new byte[] { 1 },
            -1,
            CreatedAtUtc,
            UpdatedAtUtc));
    }

    [Fact]
    public void CreateVaultFieldCommandRejectsDefaultTimestamps()
    {
        Assert.Throws<ArgumentException>(() => new CreateVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            "node",
            "username",
            new byte[] { 1 },
            0,
            default,
            UpdatedAtUtc));

        Assert.Throws<ArgumentException>(() => new CreateVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            "node",
            "username",
            new byte[] { 1 },
            0,
            CreatedAtUtc,
            default));
    }

    [Fact]
    public void CreateVaultFieldCommandRejectsUpdatedTimestampBeforeCreatedTimestamp()
    {
        Assert.Throws<ArgumentException>(() => new CreateVaultFieldCommand(
            new UnusedDbConnection(),
            "field",
            "node",
            "username",
            new byte[] { 1 },
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

    private static async Task<byte[]> ExecuteBytesAsync(DbConnection connection, string commandText)
    {
        object? result = await ExecuteScalarAsync(connection, commandText);
        return Assert.IsType<byte[]>(result);
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
