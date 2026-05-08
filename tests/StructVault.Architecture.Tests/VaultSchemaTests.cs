using StructVault.Application.Abstractions.Persistence;
using StructVault.Application.Persistence;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultSchemaTests
{
    [Fact]
    public void SchemaDefinesVaultNodeTableWithHierarchyAndValidationConstraints()
    {
        string schema = VaultSchema.CreateScript;

        Assert.Contains("CREATE TABLE IF NOT EXISTS VaultNode", schema, StringComparison.Ordinal);
        Assert.Contains("Id TEXT NOT NULL PRIMARY KEY", schema, StringComparison.Ordinal);
        Assert.Contains("ParentNodeId TEXT NULL", schema, StringComparison.Ordinal);
        Assert.Contains("Name TEXT NOT NULL CHECK (length(trim(Name)) > 0)", schema, StringComparison.Ordinal);
        Assert.Contains("SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0)", schema, StringComparison.Ordinal);
        Assert.Contains("REFERENCES VaultNode(Id) ON DELETE CASCADE", schema, StringComparison.Ordinal);
        Assert.Contains("CK_VaultNode_NoSelfParent", schema, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaDefinesVaultFieldTableLinkedToNodeWithDuplicateKeysAllowed()
    {
        string schema = VaultSchema.CreateScript;

        Assert.Contains("CREATE TABLE IF NOT EXISTS VaultField", schema, StringComparison.Ordinal);
        Assert.Contains("NodeId TEXT NOT NULL", schema, StringComparison.Ordinal);
        Assert.Contains("Key TEXT NOT NULL CHECK (length(trim(Key)) > 0)", schema, StringComparison.Ordinal);
        Assert.Contains("Value BLOB NOT NULL", schema, StringComparison.Ordinal);
        Assert.Contains("REFERENCES VaultNode(Id) ON DELETE CASCADE", schema, StringComparison.Ordinal);
        Assert.DoesNotContain("UNIQUE (NodeId, Key)", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UNIQUE(NodeId,Key)", schema, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SchemaDefinesOrderingIndexesForNodesAndFields()
    {
        string schema = VaultSchema.CreateScript;

        Assert.Contains("IX_VaultNode_ParentNodeId_SortOrder", schema, StringComparison.Ordinal);
        Assert.Contains("ON VaultNode (ParentNodeId, SortOrder, Name, Id)", schema, StringComparison.Ordinal);
        Assert.Contains("IX_VaultField_NodeId_SortOrder", schema, StringComparison.Ordinal);
        Assert.Contains("ON VaultField (NodeId, SortOrder, Id)", schema, StringComparison.Ordinal);
    }

    [Fact]
    public void SqliteSchemaProviderReturnsConfiguredSchemaScript()
    {
        SqliteVaultSchemaProvider provider = new();

        string schema = provider.GetCreateSchemaScript();

        Assert.Equal(VaultSchema.CreateScript, schema);
        Assert.False(string.IsNullOrWhiteSpace(schema));
    }

    [Fact]
    public async Task GetVaultSchemaQueryHandlerReturnsSchemaThroughApplicationAbstraction()
    {
        SqliteVaultSchemaProvider provider = new();
        GetVaultSchemaQueryHandler handler = new(provider);

        string schema = await handler.Handle(new GetVaultSchemaQuery(), CancellationToken.None);

        Assert.Equal(VaultSchema.CreateScript, schema);
    }

    [Fact]
    public async Task GetVaultSchemaQueryHandlerRejectsEmptyProviderScript()
    {
        GetVaultSchemaQueryHandler handler = new(new EmptyVaultSchemaProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(new GetVaultSchemaQuery(), CancellationToken.None));
    }

    private sealed class EmptyVaultSchemaProvider : IVaultSchemaProvider
    {
        public string GetCreateSchemaScript() => string.Empty;
    }
}
