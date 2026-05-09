using System.Collections.ObjectModel;
using System.Text;
using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Persistence;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultDynamicFieldRenderingViewModelTests
{
    [Fact]
    public async Task SelectingNodeQueriesFieldsAndMapsDisplayModels()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord root = CreateNode("node-root", "Infrastructure", timestamp);
        VaultFieldRecord endpoint = CreateField("field-endpoint", "node-root", "Endpoint", "localhost", 0, timestamp);
        VaultFieldRecord port = CreateField("field-port", "node-root", "Port", "5432", 1, timestamp);
        RecordingSender sender = new([root])
        {
            FieldsByNodeId = { ["node-root"] = new[] { endpoint, port } }
        };
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));

        Assert.IsType<ListVaultFieldsByNodeIdQuery>(sender.LastRequest);
        ListVaultFieldsByNodeIdQuery query = Assert.IsType<ListVaultFieldsByNodeIdQuery>(sender.LastRequest);
        Assert.Same(connection, query.Connection);
        Assert.Equal("node-root", query.NodeId);
        Assert.True(viewModel.HasSelectedNode);
        Assert.True(viewModel.HasSelectedFields);
        Assert.Equal("Infrastructure", viewModel.SelectedNodeName);
        Assert.Equal(new[] { "Endpoint", "Port" }, viewModel.SelectedFields.Select(field => field.Key).ToArray());
        Assert.Equal(new[] { "localhost", "5432" }, viewModel.SelectedFields.Select(field => field.DisplayValue).ToArray());
    }

    [Fact]
    public async Task SelectingDifferentNodeReplacesDetailPanelState()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord infrastructure = CreateNode("node-infrastructure", "Infrastructure", timestamp);
        VaultNodeHierarchyRecord operations = CreateNode("node-operations", "Operations", timestamp);
        RecordingSender sender = new([infrastructure, operations])
        {
            FieldsByNodeId =
            {
                ["node-infrastructure"] = new[] { CreateField("field-host", "node-infrastructure", "Host", "localhost", 0, timestamp) },
                ["node-operations"] = new[]
                {
                    CreateField("field-runbook", "node-operations", "Runbook", "restart-service", 0, timestamp),
                    CreateField("field-owner", "node-operations", "Owner", "platform", 1, timestamp)
                }
            }
        };
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        await viewModel.SelectVaultNodeAsync(viewModel.VaultNodes[0]);
        await viewModel.SelectVaultNodeAsync(viewModel.VaultNodes[1]);

        ListVaultFieldsByNodeIdQuery query = Assert.IsType<ListVaultFieldsByNodeIdQuery>(sender.LastRequest);
        Assert.Same(connection, query.Connection);
        Assert.Equal("node-operations", query.NodeId);
        Assert.Same(viewModel.VaultNodes[1], viewModel.SelectedNode);
        Assert.Equal("Operations", viewModel.SelectedNodeName);
        Assert.True(viewModel.HasSelectedNode);
        Assert.True(viewModel.HasSelectedFields);
        Assert.Equal(new[] { "Runbook", "Owner" }, viewModel.SelectedFields.Select(field => field.Key).ToArray());
        Assert.Equal(new[] { "restart-service", "platform" }, viewModel.SelectedFields.Select(field => field.DisplayValue).ToArray());
        Assert.DoesNotContain(viewModel.SelectedFields, field => field.Key == "Host");
    }

    [Fact]
    public async Task SelectingNodeWithoutFieldsKeepsDetailCollectionEmpty()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RecordingSender sender = new([CreateNode("node-root", "Empty", timestamp)]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));

        Assert.True(viewModel.HasSelectedNode);
        Assert.False(viewModel.HasSelectedFields);
        Assert.Empty(viewModel.SelectedFields);
    }

    [Fact]
    public async Task ClearingSelectionRemovesSelectedNodeAndFields()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RecordingSender sender = new([CreateNode("node-root", "Infrastructure", timestamp)])
        {
            FieldsByNodeId = { ["node-root"] = new[] { CreateField("field-one", "node-root", "Host", "localhost", 0, timestamp) } }
        };
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));

        await viewModel.SelectVaultNodeAsync(null);

        Assert.Null(viewModel.SelectedNode);
        Assert.Equal(string.Empty, viewModel.SelectedNodeName);
        Assert.False(viewModel.HasSelectedNode);
        Assert.False(viewModel.HasSelectedFields);
        Assert.Empty(viewModel.SelectedFields);
    }

    [Fact]
    public void SelectedFieldsCollectionIsReadOnly()
    {
        RecordingSender sender = new([]);
        MainWindowViewModel viewModel = new(sender);

        Assert.IsType<ReadOnlyObservableCollection<VaultFieldViewModel>>(viewModel.SelectedFields);
    }

    [Fact]
    public async Task SelectingNodeRequiresLoadedOpenConnection()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RecordingSender sender = new([]);
        MainWindowViewModel viewModel = new(sender);
        VaultTreeNodeViewModel node = new(CreateNode("node-root", "Infrastructure", timestamp));

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.SelectVaultNodeAsync(node));
    }

    [Fact]
    public async Task SelectingNodeFailsWhenLoadedConnectionWasClosed()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        RecordingSender sender = new([CreateNode("node-root", "Infrastructure", timestamp)]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        await connection.CloseAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes)));
    }

    [Fact]
    public void FieldDisplayFallsBackForNonUtf8BinaryValues()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultFieldRecord field = new(
            "field-secret",
            "node-root",
            "Attachment",
            new byte[] { 0xFF, 0xFE, 0xFD },
            0,
            timestamp,
            timestamp);

        VaultFieldViewModel viewModel = new(field);

        Assert.Equal("Attachment", viewModel.Key);
        Assert.Equal("Binary value (3 bytes)", viewModel.DisplayValue);
        Assert.Equal(3, viewModel.ValueLength);
    }

    private static VaultNodeHierarchyRecord CreateNode(string id, string name, DateTimeOffset timestamp)
    {
        return new VaultNodeHierarchyRecord(id, null, name, 0, timestamp, timestamp, Array.Empty<VaultNodeHierarchyRecord>());
    }

    private static VaultFieldRecord CreateField(
        string id,
        string nodeId,
        string key,
        string value,
        int sortOrder,
        DateTimeOffset timestamp)
    {
        return new VaultFieldRecord(id, nodeId, key, Encoding.UTF8.GetBytes(value), sortOrder, timestamp, timestamp);
    }

    private sealed class RecordingSender : ISender
    {
        public RecordingSender(IReadOnlyList<VaultNodeHierarchyRecord> hierarchy)
        {
            Hierarchy = hierarchy;
        }

        public IReadOnlyList<VaultNodeHierarchyRecord> Hierarchy { get; set; }

        public Dictionary<string, IReadOnlyList<VaultFieldRecord>> FieldsByNodeId { get; } = new(StringComparer.Ordinal);

        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (request is ListVaultNodeHierarchyQuery && Hierarchy is TResponse hierarchyResponse)
            {
                return Task.FromResult(hierarchyResponse);
            }

            if (request is ListVaultFieldsByNodeIdQuery fieldQuery)
            {
                IReadOnlyList<VaultFieldRecord> fields = FieldsByNodeId.GetValueOrDefault(fieldQuery.NodeId, Array.Empty<VaultFieldRecord>());
                if (fields is TResponse fieldResponse)
                {
                    return Task.FromResult(fieldResponse);
                }
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

            if (request is ListVaultFieldsByNodeIdQuery fieldQuery)
            {
                return Task.FromResult<object?>(FieldsByNodeId.GetValueOrDefault(fieldQuery.NodeId, Array.Empty<VaultFieldRecord>()));
            }

            throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vault dynamic field rendering tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vault dynamic field rendering tests do not use streaming requests.");
        }
    }
}
