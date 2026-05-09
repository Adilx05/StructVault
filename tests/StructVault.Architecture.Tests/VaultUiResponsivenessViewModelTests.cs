using System.Text;
using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Persistence;
using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultUiResponsivenessViewModelTests
{
    [Fact]
    public void UiResponsivenessOptionsRejectsNonPositiveBatchSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiResponsivenessOptions(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiResponsivenessOptions(-1));
    }

    [Fact]
    public async Task LoadVaultTreeYieldsBetweenConfiguredNodeBatches()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RecordingSender sender = new()
        {
            Hierarchy = Enumerable.Range(0, 3)
                .Select(index => new VaultNodeHierarchyRecord($"node-{index}", null, $"Node {index}", index, timestamp, timestamp, []))
                .ToArray()
        };
        MainWindowViewModel viewModel = new(
            sender,
            new ContextMenuInputService(),
            new UiResponsivenessOptions(collectionUpdateYieldBatchSize: 1));
        CountingSynchronizationContext synchronizationContext = new();
        SynchronizationContext? originalContext = SynchronizationContext.Current;
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        try
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

            await viewModel.LoadVaultTreeAsync(connection);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        Assert.Equal(3, viewModel.VaultNodes.Count);
        Assert.True(synchronizationContext.PostCount >= 3);
    }

    [Fact]
    public async Task SearchVaultYieldsBetweenConfiguredResultBatches()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RecordingSender sender = new()
        {
            SearchResults = Enumerable.Range(0, 3)
                .Select(index => new VaultSearchResultRecord(VaultSearchResultKind.Node, $"node-{index}", $"Node {index}", null, null, "Name"))
                .ToArray()
        };
        MainWindowViewModel viewModel = new(
            sender,
            new ContextMenuInputService(),
            new UiResponsivenessOptions(collectionUpdateYieldBatchSize: 1));
        CountingSynchronizationContext synchronizationContext = new();
        SynchronizationContext? originalContext = SynchronizationContext.Current;
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        viewModel.SearchText = "node";

        try
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

            await ((AsyncCommand)viewModel.SearchVaultCommand).ExecuteAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        Assert.Equal(3, viewModel.SearchResults.Count);
        Assert.True(synchronizationContext.PostCount >= 3);
    }

    [Fact]
    public async Task SelectVaultNodeYieldsBetweenConfiguredFieldBatches()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, timestamp, timestamp, []);
        RecordingSender sender = new()
        {
            Hierarchy = [root],
            Fields = Enumerable.Range(0, 3)
                .Select(index => new VaultFieldRecord(
                    $"field-{index}",
                    "node-root",
                    $"Field {index}",
                    Encoding.UTF8.GetBytes($"Value {index}"),
                    index,
                    timestamp,
                    timestamp))
                .ToArray()
        };
        MainWindowViewModel viewModel = new(
            sender,
            new ContextMenuInputService(),
            new UiResponsivenessOptions(collectionUpdateYieldBatchSize: 1));
        CountingSynchronizationContext synchronizationContext = new();
        SynchronizationContext? originalContext = SynchronizationContext.Current;
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        try
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

            await viewModel.SelectVaultNodeAsync(viewModel.VaultNodes.Single());
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        Assert.Equal(3, viewModel.SelectedFields.Count);
        Assert.True(synchronizationContext.PostCount >= 3);
    }

    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private int postCount;

        public int PostCount => Volatile.Read(ref postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            ArgumentNullException.ThrowIfNull(d);
            Interlocked.Increment(ref postCount);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                SynchronizationContext? originalContext = Current;
                SetSynchronizationContext(this);
                try
                {
                    d(state);
                }
                finally
                {
                    SetSynchronizationContext(originalContext);
                }
            });
        }
    }

    private sealed class RecordingSender : ISender
    {
        public IReadOnlyList<VaultNodeHierarchyRecord> Hierarchy { get; init; } = [];

        public IReadOnlyList<VaultSearchResultRecord> SearchResults { get; init; } = [];

        public IReadOnlyList<VaultFieldRecord> Fields { get; init; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request is ListVaultNodeHierarchyQuery)
            {
                return Task.FromResult((TResponse)(object)Hierarchy);
            }

            if (request is SearchVaultQuery)
            {
                return Task.FromResult((TResponse)(object)SearchResults);
            }

            if (request is ListVaultFieldsByNodeIdQuery)
            {
                return Task.FromResult((TResponse)(object)Fields);
            }

            throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return request switch
            {
                ListVaultNodeHierarchyQuery => Task.FromResult<object?>(Hierarchy),
                SearchVaultQuery => Task.FromResult<object?>(SearchResults),
                ListVaultFieldsByNodeIdQuery => Task.FromResult<object?>(Fields),
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("UI responsiveness tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("UI responsiveness tests do not use streaming requests.");
        }
    }
}
