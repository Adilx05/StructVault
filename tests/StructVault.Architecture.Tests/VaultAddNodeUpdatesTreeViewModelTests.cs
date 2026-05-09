using System.Data.Common;
using MediatR;
using StructVault.Application.Persistence;
using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultAddNodeUpdatesTreeViewModelTests
{
    [Fact]
    public async Task AddRootNodeUpdatesBoundTreeFromPersistedVaultHierarchy()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        await using DbConnection connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new() { NextNodeName = "  Infrastructure  " };
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection);

        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();

        VaultTreeNodeViewModel rootNode = Assert.Single(viewModel.VaultNodes);
        Assert.Equal("Infrastructure", rootNode.Name);
        Assert.Null(rootNode.ParentNodeId);
        Assert.Equal(0, rootNode.SortOrder);
        Assert.Empty(rootNode.Children);
        Assert.Same(rootNode, viewModel.SelectedNode);
        Assert.Equal("Infrastructure", viewModel.SelectedNodeName);
        Assert.Contains(sender.HandledRequests, request => request is CreateVaultNodeCommand);
        Assert.True(sender.HandledRequests.Count(request => request is ListVaultNodeHierarchyQuery) >= 2);
    }

    [Fact]
    public async Task AddChildNodeUpdatesBoundTreeFromPersistedVaultHierarchy()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        await using DbConnection connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new() { NextNodeName = "Root" };
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection);
        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();
        VaultTreeNodeViewModel rootNodeBeforeChildAdd = Assert.Single(viewModel.VaultNodes);
        inputService.NextNodeName = "  Database  ";

        await ((AsyncCommand)viewModel.AddChildNodeCommand).ExecuteAsync(rootNodeBeforeChildAdd);

        VaultTreeNodeViewModel rootNode = Assert.Single(viewModel.VaultNodes);
        Assert.Equal("Root", rootNode.Name);
        Assert.Null(rootNode.ParentNodeId);
        Assert.Equal(0, rootNode.SortOrder);
        VaultTreeNodeViewModel childNode = Assert.Single(rootNode.Children);
        Assert.Equal(rootNode.Id, childNode.ParentNodeId);
        Assert.Equal("Database", childNode.Name);
        Assert.Equal(0, childNode.SortOrder);
        Assert.Same(childNode, viewModel.SelectedNode);
        Assert.Equal("Database", viewModel.SelectedNodeName);
        Assert.Equal(2, sender.HandledRequests.Count(request => request is CreateVaultNodeCommand));
    }

    private sealed class RecordingContextMenuInputService : IContextMenuInputService
    {
        public string? NextNodeName { get; set; }

        public string? RequestNodeName(string title, string message, string? initialName = null)
        {
            return NextNodeName;
        }

        public VaultFieldInput? RequestField(string title, string keyMessage, string valueMessage, VaultFieldInput? initialValue = null)
        {
            throw new NotSupportedException("Add-node tree update tests do not request fields.");
        }

        public bool ConfirmDelete(string title, string message)
        {
            throw new NotSupportedException("Add-node tree update tests do not delete nodes or fields.");
        }

        public void ShowValidationError(string title, string message)
        {
            throw new InvalidOperationException($"Unexpected validation error '{title}': {message}");
        }
    }

    private sealed class PersistenceBackedSender : ISender
    {
        private readonly SqliteVaultNodeWriter nodeStore = new();
        private readonly SqliteVaultFieldWriter fieldStore = new();

        public List<object> HandledRequests { get; } = new();

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            HandledRequests.Add(request);

            object response = request switch
            {
                ListVaultNodeHierarchyQuery query => await new ListVaultNodeHierarchyQueryHandler(nodeStore)
                    .Handle(query, cancellationToken)
                    .ConfigureAwait(false),
                ListVaultFieldsByNodeIdQuery query => await new ListVaultFieldsByNodeIdQueryHandler(fieldStore)
                    .Handle(query, cancellationToken)
                    .ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };

            return (TResponse)response;
        }

        public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            ArgumentNullException.ThrowIfNull(request);
            HandledRequests.Add(request);

            switch (request)
            {
                case CreateVaultNodeCommand command:
                    await new CreateVaultNodeCommandHandler(nodeStore).Handle(command, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
            }
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
                case CreateVaultNodeCommand command:
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
            throw new NotSupportedException("Add-node tree update tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Add-node tree update tests do not use streaming requests.");
        }
    }
}
