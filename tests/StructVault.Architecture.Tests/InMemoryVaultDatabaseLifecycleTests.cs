using System.Data;
using System.Data.Common;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Persistence;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class InMemoryVaultDatabaseLifecycleTests
{
    [Fact]
    public async Task FactoryCreatesOpenInMemoryDatabaseWithVaultSchema()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);

        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(1L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'VaultNode';"));
        Assert.Equal(1L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'VaultField';"));
        Assert.Equal(1L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'VaultSetting';"));
    }

    [Fact]
    public async Task FactoryEnablesForeignKeyEnforcementForCreatedConnection()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);

        Assert.Equal(1L, await ExecuteScalarAsync(connection, "PRAGMA foreign_keys;"));
    }

    [Fact]
    public async Task FactoryKeepsDatabaseAliveUntilReturnedConnectionIsDisposed()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);

        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO VaultNode (Id, ParentNodeId, Name, SortOrder, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('root', NULL, 'Root', 0, '2026-05-08T00:00:00Z', '2026-05-08T00:00:00Z');
            """);

        Assert.Equal(1L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM VaultNode WHERE Id = 'root';"));
    }

    [Fact]
    public async Task FactoryRejectsEmptySchemaScript()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new EmptyVaultSchemaProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.CreateOpenConnectionAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateInMemoryVaultDatabaseCommandHandlerReturnsOpenInitializedConnection()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateInMemoryVaultDatabaseCommandHandler handler = new(factory);

        await using DbConnection connection = await handler.Handle(new CreateInMemoryVaultDatabaseCommand(), CancellationToken.None);

        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(1L, await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'VaultNode';"));
    }

    [Fact]
    public async Task CreateInMemoryVaultDatabaseCommandHandlerRejectsMissingConnections()
    {
        CreateInMemoryVaultDatabaseCommandHandler handler = new(new MissingConnectionFactory());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(new CreateInMemoryVaultDatabaseCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateInMemoryVaultDatabaseCommandHandlerRejectsClosedConnections()
    {
        CreateInMemoryVaultDatabaseCommandHandler handler = new(new ClosedConnectionFactory());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(new CreateInMemoryVaultDatabaseCommand(), CancellationToken.None));
    }

    private static async Task<object?> ExecuteScalarAsync(DbConnection connection, string commandText)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteNonQueryAsync(DbConnection connection, string commandText)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class EmptyVaultSchemaProvider : IVaultSchemaProvider
    {
        public string GetCreateSchemaScript() => string.Empty;
    }

    private sealed class MissingConnectionFactory : IVaultDatabaseConnectionFactory
    {
        public Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<DbConnection>(null!);
        }
    }

    private sealed class ClosedConnectionFactory : IVaultDatabaseConnectionFactory
    {
        public Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<DbConnection>(new ClosedDbConnection());
        }
    }

    private sealed class ClosedDbConnection : DbConnection
    {
        public override string? ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => ConnectionState.Closed;

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
