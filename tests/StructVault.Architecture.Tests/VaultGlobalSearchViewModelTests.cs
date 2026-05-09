using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Persistence;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultGlobalSearchViewModelTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchVaultAsyncPopulatesSearchResultsFromMediatorQuery()
    {
        RecordingSender sender = new(
            [new VaultNodeHierarchyRecord("node-1", null, "Personal", 0, Timestamp, Timestamp, Array.Empty<VaultNodeHierarchyRecord>())],
            [new VaultSearchResultRecord(VaultSearchResultKind.Node, "node-1", "Personal", null, null, "Node name")]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = await CreateOpenConnectionAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        viewModel.SearchText = "personal";
        await viewModel.SearchVaultAsync();

        VaultSearchResultViewModel result = Assert.Single(viewModel.SearchResults);
        Assert.IsType<SearchVaultQuery>(sender.LastRequest);
        Assert.Equal("Personal", result.Title);
        Assert.Equal("Node name", result.Subtitle);
        Assert.True(viewModel.HasSearchResults);
        Assert.Equal("1 search result", viewModel.SearchStatusText);
    }

    [Fact]
    public async Task ClearingSearchTextClearsSearchResults()
    {
        RecordingSender sender = new(
            [new VaultNodeHierarchyRecord("node-1", null, "Personal", 0, Timestamp, Timestamp, Array.Empty<VaultNodeHierarchyRecord>())],
            [new VaultSearchResultRecord(VaultSearchResultKind.Node, "node-1", "Personal", null, null, "Node name")]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = await CreateOpenConnectionAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        viewModel.SearchText = "personal";
        await viewModel.SearchVaultAsync();

        viewModel.SearchText = "   ";

        Assert.Empty(viewModel.SearchResults);
        Assert.False(viewModel.HasSearchResults);
        Assert.Equal("Enter text to search nodes and fields.", viewModel.SearchStatusText);
    }

    [Fact]
    public async Task SelectSearchResultSelectsMatchedNodeAndLoadsFields()
    {
        VaultNodeHierarchyRecord node = new("node-1", null, "Personal", 0, Timestamp, Timestamp, Array.Empty<VaultNodeHierarchyRecord>());
        VaultFieldRecord field = new("field-1", "node-1", "Email", [1], 0, Timestamp, Timestamp);
        RecordingSender sender = new(
            [node],
            [new VaultSearchResultRecord(VaultSearchResultKind.Field, "node-1", "Personal", "field-1", "Email", "Field key")])
        {
            FieldsByNodeId = new Dictionary<string, IReadOnlyList<VaultFieldRecord>>(StringComparer.Ordinal)
            {
                ["node-1"] = [field]
            }
        };
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = await CreateOpenConnectionAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        viewModel.SearchText = "email";
        await viewModel.SearchVaultAsync();

        await viewModel.SelectSearchResultAsync(viewModel.SearchResults[0]);

        Assert.NotNull(viewModel.SelectedNode);
        Assert.Equal("node-1", viewModel.SelectedNode.Id);
        VaultFieldViewModel selectedField = Assert.Single(viewModel.SelectedFields);
        Assert.Equal("Email", selectedField.Key);
    }

    private static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private sealed class RecordingSender : ISender
    {
        public RecordingSender(IReadOnlyList<VaultNodeHierarchyRecord> hierarchy, IReadOnlyList<VaultSearchResultRecord> searchResults)
        {
            Hierarchy = hierarchy;
            SearchResults = searchResults;
        }

        public IReadOnlyList<VaultNodeHierarchyRecord> Hierarchy { get; }

        public IReadOnlyList<VaultSearchResultRecord> SearchResults { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<VaultFieldRecord>> FieldsByNodeId { get; init; } =
            new Dictionary<string, IReadOnlyList<VaultFieldRecord>>(StringComparer.Ordinal);

        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            object response = request switch
            {
                ListVaultNodeHierarchyQuery => Hierarchy,
                SearchVaultQuery => SearchResults,
                ListVaultFieldsByNodeIdQuery query => FieldsByNodeId.TryGetValue(query.NodeId, out IReadOnlyList<VaultFieldRecord>? fields)
                    ? fields
                    : Array.Empty<VaultFieldRecord>(),
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };

            return Task.FromResult((TResponse)response);
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
            return request switch
            {
                ListVaultNodeHierarchyQuery => Task.FromResult<object?>(Hierarchy),
                SearchVaultQuery => Task.FromResult<object?>(SearchResults),
                ListVaultFieldsByNodeIdQuery query => Task.FromResult<object?>(FieldsByNodeId.TryGetValue(query.NodeId, out IReadOnlyList<VaultFieldRecord>? fields)
                    ? fields
                    : Array.Empty<VaultFieldRecord>()),
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vault global search view model tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vault global search view model tests do not use streaming requests.");
        }
    }
}
