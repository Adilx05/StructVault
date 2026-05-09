using System.Data.Common;
using System.Text;
using StructVault.Application.Persistence;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultGlobalSearchTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchVaultQueryHandlerFindsNodeByName()
    {
        SqliteVaultNodeWriter nodeStore = new();
        SqliteVaultFieldWriter fieldStore = new();
        await using DbConnection connection = await CreateSeededConnectionAsync(nodeStore, fieldStore);
        SearchVaultQueryHandler handler = new(nodeStore, fieldStore);

        IReadOnlyList<VaultSearchResultRecord> results = await handler.Handle(new SearchVaultQuery(connection, "finance"), CancellationToken.None);

        VaultSearchResultRecord result = Assert.Single(results);
        Assert.Equal(VaultSearchResultKind.Node, result.Kind);
        Assert.Equal("finance", result.NodeId);
        Assert.Equal("Finance", result.NodeName);
        Assert.Equal("Node name", result.MatchedProperty);
    }

    [Fact]
    public async Task SearchVaultQueryHandlerFindsFieldByValueWithoutReturningPlaintextValue()
    {
        SqliteVaultNodeWriter nodeStore = new();
        SqliteVaultFieldWriter fieldStore = new();
        await using DbConnection connection = await CreateSeededConnectionAsync(nodeStore, fieldStore);
        SearchVaultQueryHandler handler = new(nodeStore, fieldStore);

        IReadOnlyList<VaultSearchResultRecord> results = await handler.Handle(new SearchVaultQuery(connection, "alice@example.com"), CancellationToken.None);

        VaultSearchResultRecord result = Assert.Single(results);
        Assert.Equal(VaultSearchResultKind.Field, result.Kind);
        Assert.Equal("email-field", result.FieldId);
        Assert.Equal("Email", result.FieldKey);
        Assert.Equal("personal", result.NodeId);
        Assert.Equal("Field value", result.MatchedProperty);
    }

    [Fact]
    public async Task SearchVaultQueryHandlerSupportsCaseInsensitivePartialMatches()
    {
        SqliteVaultNodeWriter nodeStore = new();
        SqliteVaultFieldWriter fieldStore = new();
        await using DbConnection connection = await CreateSeededConnectionAsync(nodeStore, fieldStore);
        SearchVaultQueryHandler handler = new(nodeStore, fieldStore);

        IReadOnlyList<VaultSearchResultRecord> results = await handler.Handle(new SearchVaultQuery(connection, "ban"), CancellationToken.None);

        VaultSearchResultRecord result = Assert.Single(results);
        Assert.Equal(VaultSearchResultKind.Field, result.Kind);
        Assert.Equal("bank-field", result.FieldId);
        Assert.Equal("Bank Login", result.FieldKey);
        Assert.Equal("Field key", result.MatchedProperty);
    }

    [Fact]
    public async Task SearchVaultQueryHandlerTreatsLikeWildcardsAsLiteralSearchText()
    {
        SqliteVaultNodeWriter nodeStore = new();
        SqliteVaultFieldWriter fieldStore = new();
        await using DbConnection connection = await CreateSeededConnectionAsync(nodeStore, fieldStore);
        SearchVaultQueryHandler handler = new(nodeStore, fieldStore);

        IReadOnlyList<VaultSearchResultRecord> wildcardResults = await handler.Handle(new SearchVaultQuery(connection, "_"), CancellationToken.None);
        IReadOnlyList<VaultSearchResultRecord> literalResults = await handler.Handle(new SearchVaultQuery(connection, "100%"), CancellationToken.None);

        Assert.Empty(wildcardResults);
        VaultSearchResultRecord result = Assert.Single(literalResults);
        Assert.Equal("percent-field", result.FieldId);
    }


    [Fact]
    public async Task SearchVaultQueryHandlerCanFilterToNodesOnly()
    {
        SqliteVaultNodeWriter nodeStore = new();
        SqliteVaultFieldWriter fieldStore = new();
        await using DbConnection connection = await CreateSeededConnectionAsync(nodeStore, fieldStore);
        SearchVaultQueryHandler handler = new(nodeStore, fieldStore);

        IReadOnlyList<VaultSearchResultRecord> results = await handler.Handle(new SearchVaultQuery(connection, "finance", SearchVaultFilter.Nodes), CancellationToken.None);

        VaultSearchResultRecord result = Assert.Single(results);
        Assert.Equal(VaultSearchResultKind.Node, result.Kind);
        Assert.Equal("finance", result.NodeId);
    }

    [Fact]
    public async Task SearchVaultQueryHandlerCanFilterToFieldsOnly()
    {
        SqliteVaultNodeWriter nodeStore = new();
        SqliteVaultFieldWriter fieldStore = new();
        await using DbConnection connection = await CreateSeededConnectionAsync(nodeStore, fieldStore);
        SearchVaultQueryHandler handler = new(nodeStore, fieldStore);

        IReadOnlyList<VaultSearchResultRecord> results = await handler.Handle(new SearchVaultQuery(connection, "finance", SearchVaultFilter.Fields), CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public void SearchVaultQueryRejectsUnsupportedFilter()
    {
        using Microsoft.Data.Sqlite.SqliteConnection connection = new("Data Source=:memory:");

        Assert.Throws<ArgumentOutOfRangeException>(() => new SearchVaultQuery(connection, "personal", (SearchVaultFilter)99));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SearchVaultQueryRequiresSearchText(string? searchText)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SearchVaultQuery(new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:"), searchText!));
    }

    private static async Task<DbConnection> CreateSeededConnectionAsync(SqliteVaultNodeWriter nodeStore, SqliteVaultFieldWriter fieldStore)
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        CreateVaultNodeCommandHandler createNode = new(nodeStore);
        CreateVaultFieldCommandHandler createField = new(fieldStore);

        await createNode.Handle(new CreateVaultNodeCommand(connection, "personal", null, "Personal", 0, Timestamp, Timestamp), CancellationToken.None);
        await createNode.Handle(new CreateVaultNodeCommand(connection, "finance", null, "Finance", 1, Timestamp, Timestamp), CancellationToken.None);
        await createField.Handle(
            new CreateVaultFieldCommand(connection, "email-field", "personal", "Email", Encoding.UTF8.GetBytes("alice@example.com"), 0, Timestamp, Timestamp),
            CancellationToken.None);
        await createField.Handle(
            new CreateVaultFieldCommand(connection, "bank-field", "finance", "Bank Login", Encoding.UTF8.GetBytes("vault-user"), 0, Timestamp, Timestamp),
            CancellationToken.None);
        await createField.Handle(
            new CreateVaultFieldCommand(connection, "percent-field", "finance", "Discount", Encoding.UTF8.GetBytes("Save 100% offline"), 1, Timestamp, Timestamp),
            CancellationToken.None);
        await createField.Handle(
            new CreateVaultFieldCommand(connection, "binary-field", "finance", "Binary", new byte[] { 0xff, 0x00, 0x13 }, 2, Timestamp, Timestamp),
            CancellationToken.None);

        return connection;
    }
}
