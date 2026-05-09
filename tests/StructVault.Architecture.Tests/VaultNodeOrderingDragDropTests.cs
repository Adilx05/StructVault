using System.Data;
using System.Data.Common;
using MediatR;
using StructVault.Application.Persistence;
using StructVault.Desktop.ViewModels;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultNodeOrderingDragDropTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 5, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReorderedAtUtc = new(2026, 5, 9, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReorderVaultNodeCommandHandlerMovesRootNodeAndNormalizesSiblingSortOrders()
    {
        SqliteVaultNodeWriter nodeStore = new();
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler createNodeHandler = new(nodeStore);
        ReorderVaultNodeCommandHandler reorderHandler = new(nodeStore);
        ListVaultNodeHierarchyQueryHandler listHandler = new(nodeStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await CreateNodeAsync(createNodeHandler, connection, "alpha", null, "Alpha", 0);
        await CreateNodeAsync(createNodeHandler, connection, "bravo", null, "Bravo", 1);
        await CreateNodeAsync(createNodeHandler, connection, "charlie", null, "Charlie", 2);

        bool reordered = await reorderHandler.Handle(
            new ReorderVaultNodeCommand(connection, "charlie", 0, ReorderedAtUtc),
            CancellationToken.None);

        IReadOnlyList<VaultNodeHierarchyRecord> nodes = await listHandler.Handle(
            new ListVaultNodeHierarchyQuery(connection),
            CancellationToken.None);

        Assert.True(reordered);
        Assert.Equal(new[] { "charlie", "alpha", "bravo" }, nodes.Select(node => node.Id));
        Assert.Equal(new[] { 0, 1, 2 }, nodes.Select(node => node.SortOrder));
        Assert.All(nodes, node => Assert.Equal(ReorderedAtUtc, node.UpdatedAtUtc));
    }

    [Fact]
    public async Task ReorderVaultNodeCommandHandlerClampsTargetSortOrderAndDoesNotAffectOtherParents()
    {
        SqliteVaultNodeWriter nodeStore = new();
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        CreateVaultNodeCommandHandler createNodeHandler = new(nodeStore);
        ReorderVaultNodeCommandHandler reorderHandler = new(nodeStore);
        ListVaultNodeHierarchyQueryHandler listHandler = new(nodeStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        await CreateNodeAsync(createNodeHandler, connection, "root-a", null, "Root A", 0);
        await CreateNodeAsync(createNodeHandler, connection, "root-b", null, "Root B", 1);
        await CreateNodeAsync(createNodeHandler, connection, "a-one", "root-a", "One", 0);
        await CreateNodeAsync(createNodeHandler, connection, "a-two", "root-a", "Two", 1);
        await CreateNodeAsync(createNodeHandler, connection, "a-three", "root-a", "Three", 2);
        await CreateNodeAsync(createNodeHandler, connection, "b-one", "root-b", "One", 25);
        await CreateNodeAsync(createNodeHandler, connection, "b-two", "root-b", "Two", 26);

        bool reordered = await reorderHandler.Handle(
            new ReorderVaultNodeCommand(connection, "a-one", 99, ReorderedAtUtc),
            CancellationToken.None);

        IReadOnlyList<VaultNodeHierarchyRecord> roots = await listHandler.Handle(
            new ListVaultNodeHierarchyQuery(connection),
            CancellationToken.None);
        VaultNodeHierarchyRecord rootA = roots.Single(node => node.Id == "root-a");
        VaultNodeHierarchyRecord rootB = roots.Single(node => node.Id == "root-b");

        Assert.True(reordered);
        Assert.Equal(new[] { "a-two", "a-three", "a-one" }, rootA.Children.Select(node => node.Id));
        Assert.Equal(new[] { 0, 1, 2 }, rootA.Children.Select(node => node.SortOrder));
        Assert.Equal(new[] { "b-one", "b-two" }, rootB.Children.Select(node => node.Id));
        Assert.Equal(new[] { 25, 26 }, rootB.Children.Select(node => node.SortOrder));
        Assert.All(rootB.Children, node => Assert.Equal(CreatedAtUtc, node.UpdatedAtUtc));
    }

    [Fact]
    public async Task ReorderVaultNodeCommandHandlerReturnsFalseForMissingNode()
    {
        SqliteVaultNodeWriter nodeStore = new();
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        ReorderVaultNodeCommandHandler reorderHandler = new(nodeStore);

        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);

        bool reordered = await reorderHandler.Handle(
            new ReorderVaultNodeCommand(connection, "missing-node", 0, ReorderedAtUtc),
            CancellationToken.None);

        Assert.False(reordered);
    }

    [Fact]
    public async Task ReorderVaultNodeAsyncUsesCommandRefreshesSelectionAndMarksVaultDirty()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new();
        CreateVaultNodeCommandHandler createNodeHandler = new(sender.NodeStore);
        await CreateNodeAsync(createNodeHandler, connection, "alpha", null, "Alpha", 0);
        await CreateNodeAsync(createNodeHandler, connection, "bravo", null, "Bravo", 1);
        await CreateNodeAsync(createNodeHandler, connection, "charlie", null, "Charlie", 2);
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection);

        VaultTreeNodeViewModel draggedNode = viewModel.VaultNodes.Single(node => node.Id == "charlie");
        VaultTreeNodeViewModel targetNode = viewModel.VaultNodes.Single(node => node.Id == "alpha");
        bool reordered = await viewModel.ReorderVaultNodeAsync(draggedNode, targetNode);

        Assert.True(reordered);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(new[] { "charlie", "alpha", "bravo" }, viewModel.VaultNodes.Select(node => node.Id));
        Assert.Equal("charlie", viewModel.SelectedNode?.Id);
        Assert.Contains(sender.HandledRequests, request => request is ReorderVaultNodeCommand);
    }

    [Fact]
    public async Task ReorderVaultNodeAsyncRejectsDifferentParentsWithoutPersisting()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new();
        CreateVaultNodeCommandHandler createNodeHandler = new(sender.NodeStore);
        await CreateNodeAsync(createNodeHandler, connection, "root-a", null, "Root A", 0);
        await CreateNodeAsync(createNodeHandler, connection, "root-b", null, "Root B", 1);
        await CreateNodeAsync(createNodeHandler, connection, "child-a", "root-a", "Child A", 0);
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection);

        VaultTreeNodeViewModel draggedNode = viewModel.VaultNodes.Single(node => node.Id == "root-a").Children.Single();
        VaultTreeNodeViewModel targetNode = viewModel.VaultNodes.Single(node => node.Id == "root-b");
        bool reordered = await viewModel.ReorderVaultNodeAsync(draggedNode, targetNode);

        Assert.False(reordered);
        Assert.False(viewModel.IsDirty);
        Assert.DoesNotContain(sender.HandledRequests, request => request is ReorderVaultNodeCommand);
        Assert.Equal("Reorder unavailable", inputService.LastValidationTitle);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReorderVaultNodeCommandRejectsMissingNodeId(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ReorderVaultNodeCommand(
            new UnusedDbConnection(),
            id!,
            0,
            ReorderedAtUtc));
    }

    [Fact]
    public void ReorderVaultNodeCommandRejectsNegativeTargetSortOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReorderVaultNodeCommand(
            new UnusedDbConnection(),
            "node",
            -1,
            ReorderedAtUtc));
    }

    [Fact]
    public void ReorderVaultNodeCommandRejectsDefaultUpdatedTimestamp()
    {
        Assert.Throws<ArgumentException>(() => new ReorderVaultNodeCommand(
            new UnusedDbConnection(),
            "node",
            0,
            default));
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
            new CreateVaultNodeCommand(connection, id, parentNodeId, name, sortOrder, CreatedAtUtc, CreatedAtUtc),
            CancellationToken.None);
    }

    private sealed class RecordingContextMenuInputService : IContextMenuInputService
    {
        public string? LastValidationTitle { get; private set; }

        public string? RequestNodeName(string title, string message, string? initialName = null)
        {
            throw new NotSupportedException();
        }

        public VaultFieldInput? RequestField(string title, string keyMessage, string valueMessage, VaultFieldInput? initialValue = null)
        {
            throw new NotSupportedException();
        }

        public string? RequestPassword(string title, string message)
        {
            throw new NotSupportedException();
        }

        public bool ConfirmDelete(string title, string message)
        {
            throw new NotSupportedException();
        }

        public UnsavedChangesExitChoice PromptUnsavedChangesOnExit(bool canSave)
        {
            throw new NotSupportedException();
        }

        public void ShowValidationError(string title, string message)
        {
            LastValidationTitle = title;
        }
    }

    private sealed class PersistenceBackedSender : ISender
    {
        public SqliteVaultNodeWriter NodeStore { get; } = new();

        public List<object> HandledRequests { get; } = new();

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            HandledRequests.Add(request);

            object? response = request switch
            {
                ListVaultNodeHierarchyQuery query => await new ListVaultNodeHierarchyQueryHandler(NodeStore)
                    .Handle(query, cancellationToken)
                    .ConfigureAwait(false),
                ListVaultFieldsByNodeIdQuery => Array.Empty<VaultFieldRecord>(),
                ReorderVaultNodeCommand command => await new ReorderVaultNodeCommandHandler(NodeStore)
                    .Handle(command, cancellationToken)
                    .ConfigureAwait(false),
                GetClipboardSettingsQuery => ClipboardSettingsRecord.Default,
                GetIdleLockSettingsQuery => IdleLockSettingsRecord.Default,
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };

            return (TResponse)response;
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            ArgumentNullException.ThrowIfNull(request);
            HandledRequests.Add(request);
            throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
        }

        public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            switch (request)
            {
                case ListVaultNodeHierarchyQuery query:
                    return await Send<IReadOnlyList<VaultNodeHierarchyRecord>>(query, cancellationToken).ConfigureAwait(false);
                case ListVaultFieldsByNodeIdQuery query:
                    return await Send<IReadOnlyList<VaultFieldRecord>>(query, cancellationToken).ConfigureAwait(false);
                case ReorderVaultNodeCommand command:
                    return await Send<bool>(command, cancellationToken).ConfigureAwait(false);
                case GetClipboardSettingsQuery query:
                    return await Send<ClipboardSettingsRecord>(query, cancellationToken).ConfigureAwait(false);
                case GetIdleLockSettingsQuery query:
                    return await Send<IdleLockSettingsRecord>(query, cancellationToken).ConfigureAwait(false);
                case IRequest command:
                    await Send(command, cancellationToken).ConfigureAwait(false);
                    return null;
                default:
                    throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
            }
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
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
