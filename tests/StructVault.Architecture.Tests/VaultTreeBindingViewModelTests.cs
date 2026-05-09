using System.Collections.ObjectModel;
using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Persistence;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultTreeBindingViewModelTests
{
    [Fact]
    public async Task LoadVaultTreeQueriesHierarchyAndMapsNestedNodes()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord child = new(
            "node-child",
            "node-root",
            "Database",
            0,
            timestamp,
            timestamp,
            Array.Empty<VaultNodeHierarchyRecord>());
        VaultNodeHierarchyRecord root = new(
            "node-root",
            null,
            "Infrastructure",
            0,
            timestamp,
            timestamp,
            [child]);
        RecordingSender sender = new([root]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        await viewModel.LoadVaultTreeAsync(connection);

        Assert.IsType<ListVaultNodeHierarchyQuery>(sender.LastRequest);
        VaultTreeNodeViewModel rootNode = Assert.Single(viewModel.VaultNodes);
        Assert.Equal("node-root", rootNode.Id);
        Assert.Equal("Infrastructure", rootNode.Name);
        VaultTreeNodeViewModel childNode = Assert.Single(rootNode.Children);
        Assert.Equal("node-child", childNode.Id);
        Assert.Equal("node-root", childNode.ParentNodeId);
        Assert.Equal("Database", childNode.Name);
    }

    [Fact]
    public async Task LoadVaultTreeReplacesExistingNodesWithLatestHierarchy()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RecordingSender sender = new([
            new VaultNodeHierarchyRecord("node-one", null, "One", 0, timestamp, timestamp, Array.Empty<VaultNodeHierarchyRecord>())
        ]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        sender.Hierarchy = [
            new VaultNodeHierarchyRecord("node-two", null, "Two", 0, timestamp, timestamp, Array.Empty<VaultNodeHierarchyRecord>())
        ];
        await viewModel.LoadVaultTreeAsync(connection);

        VaultTreeNodeViewModel node = Assert.Single(viewModel.VaultNodes);
        Assert.Equal("node-two", node.Id);
        Assert.Equal("Two", node.Name);
    }

    [Fact]
    public async Task LoadVaultTreeClearsNodesWhenHierarchyIsEmpty()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RecordingSender sender = new([
            new VaultNodeHierarchyRecord("node-one", null, "One", 0, timestamp, timestamp, Array.Empty<VaultNodeHierarchyRecord>())
        ]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        sender.Hierarchy = [];
        await viewModel.LoadVaultTreeAsync(connection);

        Assert.Empty(viewModel.VaultNodes);
    }

    [Fact]
    public void MainWindowViewModelRequiresSender()
    {
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(null!));
    }

    [Fact]
    public async Task LoadVaultTreeRequiresOpenConnection()
    {
        RecordingSender sender = new([]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.LoadVaultTreeAsync(connection));
    }

    [Fact]
    public void VaultTreeNodeRequiresRecord()
    {
        Assert.Throws<ArgumentNullException>(() => new VaultTreeNodeViewModel(null!));
    }

    [Fact]
    public void VaultTreeNodeExposesReadOnlyChildren()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord node = new(
            "node-root",
            null,
            "Root",
            0,
            timestamp,
            timestamp,
            Array.Empty<VaultNodeHierarchyRecord>());

        VaultTreeNodeViewModel viewModel = new(node);

        Assert.IsType<ReadOnlyObservableCollection<VaultTreeNodeViewModel>>(viewModel.Children);
    }

    private sealed class RecordingSender : ISender
    {
        public RecordingSender(IReadOnlyList<VaultNodeHierarchyRecord> hierarchy)
        {
            Hierarchy = hierarchy;
        }

        public IReadOnlyList<VaultNodeHierarchyRecord> Hierarchy { get; set; }

        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (request is ListVaultNodeHierarchyQuery && Hierarchy is TResponse response)
            {
                return Task.FromResult(response);
            }

            throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            LastRequest = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (request is ListVaultNodeHierarchyQuery)
            {
                return Task.FromResult<object?>(Hierarchy);
            }

            throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vault tree binding tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vault tree binding tests do not use streaming requests.");
        }
    }
}
