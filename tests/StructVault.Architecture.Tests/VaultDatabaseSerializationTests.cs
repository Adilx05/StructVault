using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Persistence;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultDatabaseSerializationTests
{
    [Fact]
    public async Task SerializerRoundTripsVaultDatabaseContent()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultDatabaseSerializer serializer = new(new SqliteVaultSchemaProvider());

        await using DbConnection sourceConnection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await InsertSampleVaultAsync(sourceConnection);

        byte[] databaseImage = await serializer.SerializeAsync(sourceConnection, CancellationToken.None);

        await using DbConnection restoredConnection = await serializer.DeserializeAsync(databaseImage, CancellationToken.None);

        Assert.NotEmpty(databaseImage);
        Assert.Equal(2L, await ExecuteScalarAsync(restoredConnection, "SELECT COUNT(*) FROM VaultNode;"));
        Assert.Equal(2L, await ExecuteScalarAsync(restoredConnection, "SELECT COUNT(*) FROM VaultField;"));
        Assert.Equal("Root", await ExecuteScalarAsync(restoredConnection, "SELECT Name FROM VaultNode WHERE Id = 'root';"));
        Assert.Equal("Child", await ExecuteScalarAsync(restoredConnection, "SELECT Name FROM VaultNode WHERE Id = 'child';"));
        Assert.Equal(new byte[] { 1, 2, 3 }, await ExecuteBytesAsync(restoredConnection, "SELECT Value FROM VaultField WHERE Id = 'field-1';"));
        Assert.Equal(1L, await ExecuteScalarAsync(restoredConnection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'VaultSetting';"));
    }

    [Fact]
    public async Task SerializerPreservesOrderingAndForeignKeyCascadeBehavior()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultDatabaseSerializer serializer = new(new SqliteVaultSchemaProvider());

        await using DbConnection sourceConnection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await InsertSampleVaultAsync(sourceConnection);

        byte[] databaseImage = await serializer.SerializeAsync(sourceConnection, CancellationToken.None);

        await using DbConnection restoredConnection = await serializer.DeserializeAsync(databaseImage, CancellationToken.None);

        Assert.Equal("child", await ExecuteScalarAsync(restoredConnection, "SELECT Id FROM VaultNode WHERE ParentNodeId = 'root' ORDER BY SortOrder, Name, Id LIMIT 1;"));
        Assert.Equal("field-1", await ExecuteScalarAsync(restoredConnection, "SELECT Id FROM VaultField WHERE NodeId = 'child' ORDER BY SortOrder, Id LIMIT 1;"));

        await ExecuteNonQueryAsync(restoredConnection, "DELETE FROM VaultNode WHERE Id = 'child';");

        Assert.Equal(0L, await ExecuteScalarAsync(restoredConnection, "SELECT COUNT(*) FROM VaultField WHERE NodeId = 'child';"));
    }

    [Fact]
    public async Task SerializeVaultDatabaseQueryHandlerReturnsDatabaseImage()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultDatabaseSerializer serializer = new(new SqliteVaultSchemaProvider());
        SerializeVaultDatabaseQueryHandler handler = new(serializer);

        await using DbConnection sourceConnection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await InsertSampleVaultAsync(sourceConnection);

        byte[] databaseImage = await handler.Handle(new SerializeVaultDatabaseQuery(sourceConnection), CancellationToken.None);

        Assert.NotEmpty(databaseImage);
    }

    [Fact]
    public async Task DeserializeVaultDatabaseCommandHandlerReturnsOpenConnection()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        SqliteVaultDatabaseSerializer serializer = new(new SqliteVaultSchemaProvider());
        DeserializeVaultDatabaseCommandHandler handler = new(serializer);

        await using DbConnection sourceConnection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        byte[] databaseImage = await serializer.SerializeAsync(sourceConnection, CancellationToken.None);

        await using DbConnection restoredConnection = await handler.Handle(new DeserializeVaultDatabaseCommand(databaseImage), CancellationToken.None);

        Assert.Equal(ConnectionState.Open, restoredConnection.State);
        Assert.Equal(1L, await ExecuteScalarAsync(restoredConnection, "PRAGMA foreign_keys;"));
    }

    [Fact]
    public async Task SerializerRejectsClosedConnections()
    {
        SqliteVaultDatabaseSerializer serializer = new(new SqliteVaultSchemaProvider());
        await using SqliteConnection connection = new("Data Source=:memory:");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await serializer.SerializeAsync(connection, CancellationToken.None));
    }

    [Fact]
    public async Task SerializerRejectsNonSqliteConnections()
    {
        SqliteVaultDatabaseSerializer serializer = new(new SqliteVaultSchemaProvider());

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await serializer.SerializeAsync(new UnsupportedConnection(), CancellationToken.None));
    }

    [Fact]
    public async Task DeserializerRejectsInvalidDatabaseImage()
    {
        SqliteVaultDatabaseSerializer serializer = new(new SqliteVaultSchemaProvider());

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await serializer.DeserializeAsync(new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
    }

    [Fact]
    public async Task DeserializerRejectsEmptySchemaProvider()
    {
        SqliteVaultDatabaseSerializer serializer = new(new EmptyVaultSchemaProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await serializer.DeserializeAsync(new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
    }

    [Fact]
    public void DeserializeVaultDatabaseCommandRejectsEmptyDatabaseImage()
    {
        Assert.Throws<ArgumentException>(() => new DeserializeVaultDatabaseCommand(Array.Empty<byte>()));
    }

    private static async Task InsertSampleVaultAsync(DbConnection connection)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO VaultNode (Id, ParentNodeId, Name, SortOrder, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ('root', NULL, 'Root', 0, '2026-05-08T00:00:00Z', '2026-05-08T00:00:00Z'),
                ('child', 'root', 'Child', 1, '2026-05-08T00:00:00Z', '2026-05-08T00:00:00Z');

            INSERT INTO VaultField (Id, NodeId, Key, Value, SortOrder, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ('field-2', 'child', 'username', X'040506', 1, '2026-05-08T00:00:00Z', '2026-05-08T00:00:00Z'),
                ('field-1', 'child', 'username', X'010203', 0, '2026-05-08T00:00:00Z', '2026-05-08T00:00:00Z');
            """);
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

    private sealed class UnsupportedConnection : DbConnection
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
