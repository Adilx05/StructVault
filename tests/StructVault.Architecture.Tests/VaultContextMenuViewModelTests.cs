using System.Text;
using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Persistence;
using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultContextMenuViewModelTests
{
    [Fact]
    public async Task NodeContextCommandsRequireLoadedOpenConnectionAndNodeParameter()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord root = CreateNode("node-root", null, "Root", 0, timestamp);
        RecordingSender sender = new([root]);
        MainWindowViewModel viewModel = new(sender, new RecordingContextMenuInputService());
        VaultTreeNodeViewModel node = new(root);

        Assert.False(viewModel.AddRootNodeCommand.CanExecute(null));
        Assert.False(viewModel.AddChildNodeCommand.CanExecute(node));

        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        Assert.True(viewModel.AddRootNodeCommand.CanExecute(null));
        Assert.True(viewModel.AddChildNodeCommand.CanExecute(Assert.Single(viewModel.VaultNodes)));
        Assert.False(viewModel.AddChildNodeCommand.CanExecute(null));
    }

    [Fact]
    public async Task AddChildNodeDispatchesCreateCommandThroughMediatorAndRefreshesTree()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord root = CreateNode("node-root", null, "Root", 7, timestamp);
        RecordingSender sender = new([root]);
        RecordingContextMenuInputService inputService = new() { NextNodeName = "  Child node  " };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        sender.Requests.Clear();

        await ((AsyncCommand)viewModel.AddChildNodeCommand).ExecuteAsync(Assert.Single(viewModel.VaultNodes));

        CreateVaultNodeCommand command = Assert.Single(sender.Requests.OfType<CreateVaultNodeCommand>());
        Assert.Same(connection, command.Connection);
        Assert.Equal("node-root", command.ParentNodeId);
        Assert.Equal("Child node", command.Name);
        Assert.Equal(0, command.SortOrder);
        Assert.Contains(sender.Requests, request => request is ListVaultNodeHierarchyQuery);
    }

    [Fact]
    public async Task RenameNodeDispatchesUpdateCommandWhenInputIsValid()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord root = CreateNode("node-root", null, "Root", 3, timestamp);
        RecordingSender sender = new([root]);
        RecordingContextMenuInputService inputService = new() { NextNodeName = "  Renamed root  " };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        sender.Requests.Clear();

        await ((AsyncCommand)viewModel.RenameNodeCommand).ExecuteAsync(Assert.Single(viewModel.VaultNodes));

        UpdateVaultNodeCommand command = Assert.Single(sender.Requests.OfType<UpdateVaultNodeCommand>());
        Assert.Same(connection, command.Connection);
        Assert.Equal("node-root", command.Id);
        Assert.Equal("Renamed root", command.Name);
        Assert.Equal(3, command.SortOrder);
    }

    [Fact]
    public async Task NodeCommandsDoNotDispatchMutationsWhenUserCancelsOrProvidesWhitespace()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord root = CreateNode("node-root", null, "Root", 0, timestamp);
        RecordingSender sender = new([root]);
        RecordingContextMenuInputService inputService = new() { NextNodeName = "   " };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        sender.Requests.Clear();

        await ((AsyncCommand)viewModel.RenameNodeCommand).ExecuteAsync(Assert.Single(viewModel.VaultNodes));

        Assert.Empty(sender.Requests.OfType<UpdateVaultNodeCommand>());
        Assert.Equal("Node name", inputService.LastValidationTitle);
        Assert.Equal("Node names cannot be empty.", inputService.LastValidationMessage);
    }

    [Fact]
    public async Task AddFieldDispatchesCreateFieldCommandThroughMediator()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord root = CreateNode("node-root", null, "Root", 0, timestamp);
        RecordingSender sender = new([root]);
        RecordingContextMenuInputService inputService = new()
        {
            NextFieldInput = new VaultFieldInput("  Username  ", "  admin  ")
        };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        sender.Requests.Clear();

        await ((AsyncCommand)viewModel.AddFieldCommand).ExecuteAsync(Assert.Single(viewModel.VaultNodes));

        CreateVaultFieldCommand command = Assert.Single(sender.Requests.OfType<CreateVaultFieldCommand>());
        Assert.Same(connection, command.Connection);
        Assert.Equal("node-root", command.NodeId);
        Assert.Equal("Username", command.Key);
        Assert.Equal("admin", Encoding.UTF8.GetString(command.Value));
        Assert.Equal(0, command.SortOrder);
        Assert.Contains(sender.Requests, request => request is ListVaultFieldsByNodeIdQuery);
    }

    [Fact]
    public async Task EditAndDeleteFieldDispatchMediatorCommandsWhenConfirmed()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord root = CreateNode("node-root", null, "Root", 0, timestamp);
        VaultFieldRecord field = CreateField("field-one", "node-root", "Username", "admin", 4, timestamp);
        RecordingSender sender = new([root])
        {
            FieldsByNodeId = { ["node-root"] = new[] { field } }
        };
        RecordingContextMenuInputService inputService = new()
        {
            ConfirmDeleteResult = true,
            NextFieldInput = new VaultFieldInput("Login", "root")
        };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));
        VaultFieldViewModel selectedField = Assert.Single(viewModel.SelectedFields);
        sender.Requests.Clear();

        await ((AsyncCommand)viewModel.EditFieldCommand).ExecuteAsync(selectedField);
        await ((AsyncCommand)viewModel.DeleteFieldCommand).ExecuteAsync(selectedField);

        UpdateVaultFieldCommand updateCommand = Assert.Single(sender.Requests.OfType<UpdateVaultFieldCommand>());
        Assert.Equal("field-one", updateCommand.Id);
        Assert.Equal("Login", updateCommand.Key);
        Assert.Equal("root", Encoding.UTF8.GetString(updateCommand.Value));
        Assert.Equal(4, updateCommand.SortOrder);
        DeleteVaultFieldCommand deleteCommand = Assert.Single(sender.Requests.OfType<DeleteVaultFieldCommand>());
        Assert.Equal("field-one", deleteCommand.Id);
    }

    [Fact]
    public async Task DeleteNodeDoesNotDispatchWhenUserDeclinesConfirmation()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord root = CreateNode("node-root", null, "Root", 0, timestamp);
        RecordingSender sender = new([root]);
        RecordingContextMenuInputService inputService = new() { ConfirmDeleteResult = false };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        sender.Requests.Clear();

        await ((AsyncCommand)viewModel.DeleteNodeCommand).ExecuteAsync(Assert.Single(viewModel.VaultNodes));

        Assert.Empty(sender.Requests.OfType<DeleteVaultNodeCommand>());
    }

    private static VaultNodeHierarchyRecord CreateNode(string id, string? parentNodeId, string name, int sortOrder, DateTimeOffset timestamp)
    {
        return new VaultNodeHierarchyRecord(id, parentNodeId, name, sortOrder, timestamp, timestamp, Array.Empty<VaultNodeHierarchyRecord>());
    }

    private static VaultFieldRecord CreateField(string id, string nodeId, string key, string value, int sortOrder, DateTimeOffset timestamp)
    {
        return new VaultFieldRecord(id, nodeId, key, Encoding.UTF8.GetBytes(value), sortOrder, timestamp, timestamp);
    }

    private sealed class RecordingContextMenuInputService : IContextMenuInputService
    {
        public string? NextNodeName { get; set; }

        public VaultFieldInput? NextFieldInput { get; set; }

        public bool ConfirmDeleteResult { get; set; }

        public string? LastValidationTitle { get; private set; }

        public string? LastValidationMessage { get; private set; }

        public string? RequestNodeName(string title, string message, string? initialName = null)
        {
            return NextNodeName;
        }

        public VaultFieldInput? RequestField(string title, string keyMessage, string valueMessage, VaultFieldInput? initialValue = null)
        {
            return NextFieldInput;
        }

        public string? RequestPassword(string title, string message)
        {
            return null;
        }

        public bool ConfirmDelete(string title, string message)
        {
            return ConfirmDeleteResult;
        }

        public UnsavedChangesExitChoice PromptUnsavedChangesOnExit(bool canSave)
        {
            throw new NotSupportedException("This test does not confirm window exit.");
        }

        public void ShowValidationError(string title, string message)
        {
            LastValidationTitle = title;
            LastValidationMessage = message;
        }
    }

    private sealed class RecordingSender : ISender
    {
        public RecordingSender(IReadOnlyList<VaultNodeHierarchyRecord> hierarchy)
        {
            Hierarchy = hierarchy;
        }

        public IReadOnlyList<VaultNodeHierarchyRecord> Hierarchy { get; set; }

        public Dictionary<string, IReadOnlyList<VaultFieldRecord>> FieldsByNodeId { get; } = new(StringComparer.Ordinal);

        public List<object> Requests { get; } = new();

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
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

            if (request is UpdateVaultNodeCommand or UpdateVaultFieldCommand or ReorderVaultFieldCommand)
            {
                object response = true;
                return Task.FromResult((TResponse)response);
            }

            throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request is ListVaultNodeHierarchyQuery)
            {
                return Task.FromResult<object?>(Hierarchy);
            }

            if (request is ListVaultFieldsByNodeIdQuery fieldQuery)
            {
                return Task.FromResult<object?>(FieldsByNodeId.GetValueOrDefault(fieldQuery.NodeId, Array.Empty<VaultFieldRecord>()));
            }

            if (request is UpdateVaultNodeCommand or UpdateVaultFieldCommand or ReorderVaultFieldCommand)
            {
                return Task.FromResult<object?>(true);
            }

            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vault context menu tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Vault context menu tests do not use streaming requests.");
        }
    }
}
