using System.Data;
using System.Data.Common;
using System.Text;
using MediatR;
using StructVault.Application.Persistence;
using StructVault.Desktop.ViewModels;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultFieldOrderingDragDropTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 5, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReorderVaultFieldAsyncUsesCommandRefreshesFieldsAndMarksVaultDirty()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new();
        CreateVaultNodeCommandHandler createNodeHandler = new(sender.NodeStore);
        CreateVaultFieldCommandHandler createFieldHandler = new(sender.FieldStore);
        await CreateNodeAsync(createNodeHandler, connection, "root", null, "Root", 0);
        await CreateFieldAsync(createFieldHandler, connection, "alpha", "root", "Alpha", "one", 0);
        await CreateFieldAsync(createFieldHandler, connection, "bravo", "root", "Bravo", "two", 1);
        await CreateFieldAsync(createFieldHandler, connection, "charlie", "root", "Charlie", "three", 2);
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection);
        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));

        VaultFieldViewModel draggedField = viewModel.SelectedFields.Single(field => field.Id == "charlie");
        VaultFieldViewModel targetField = viewModel.SelectedFields.Single(field => field.Id == "alpha");
        bool reordered = await viewModel.ReorderVaultFieldAsync(draggedField, targetField);

        Assert.True(reordered);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(new[] { "charlie", "alpha", "bravo" }, viewModel.SelectedFields.Select(field => field.Id));
        Assert.Equal(new[] { 0, 1, 2 }, viewModel.SelectedFields.Select(field => field.SortOrder));
        Assert.Equal("root", viewModel.SelectedNode?.Id);
        Assert.Contains(sender.HandledRequests, request => request is ReorderVaultFieldCommand);
    }

    [Fact]
    public async Task ReorderVaultFieldAsyncRejectsFieldsOutsideSelectedNodeWithoutPersisting()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new();
        CreateVaultNodeCommandHandler createNodeHandler = new(sender.NodeStore);
        CreateVaultFieldCommandHandler createFieldHandler = new(sender.FieldStore);
        await CreateNodeAsync(createNodeHandler, connection, "root-a", null, "Root A", 0);
        await CreateNodeAsync(createNodeHandler, connection, "root-b", null, "Root B", 1);
        await CreateFieldAsync(createFieldHandler, connection, "field-a", "root-a", "A", "one", 0);
        await CreateFieldAsync(createFieldHandler, connection, "field-b", "root-b", "B", "two", 0);
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection);
        await viewModel.SelectVaultNodeAsync(viewModel.VaultNodes.Single(node => node.Id == "root-a"));

        VaultFieldViewModel draggedField = Assert.Single(viewModel.SelectedFields);
        VaultFieldViewModel targetField = new(new VaultFieldRecord(
            "field-b",
            "root-b",
            "B",
            Encoding.UTF8.GetBytes("two"),
            0,
            CreatedAtUtc,
            CreatedAtUtc));
        bool reordered = await viewModel.ReorderVaultFieldAsync(draggedField, targetField);

        Assert.False(reordered);
        Assert.False(viewModel.IsDirty);
        Assert.Equal(new[] { "field-a" }, viewModel.SelectedFields.Select(field => field.Id));
        Assert.DoesNotContain(sender.HandledRequests, request => request is ReorderVaultFieldCommand);
        Assert.Equal("Reorder unavailable", inputService.LastValidationTitle);
    }

    [Fact]
    public async Task ReorderVaultFieldAsyncReturnsFalseForSameFieldWithoutPersisting()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        await using DbConnection connection = await factory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new();
        CreateVaultNodeCommandHandler createNodeHandler = new(sender.NodeStore);
        CreateVaultFieldCommandHandler createFieldHandler = new(sender.FieldStore);
        await CreateNodeAsync(createNodeHandler, connection, "root", null, "Root", 0);
        await CreateFieldAsync(createFieldHandler, connection, "alpha", "root", "Alpha", "one", 0);
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection);
        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));

        VaultFieldViewModel field = Assert.Single(viewModel.SelectedFields);
        bool reordered = await viewModel.ReorderVaultFieldAsync(field, field);

        Assert.False(reordered);
        Assert.False(viewModel.IsDirty);
        Assert.DoesNotContain(sender.HandledRequests, request => request is ReorderVaultFieldCommand);
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

    private static async Task CreateFieldAsync(
        CreateVaultFieldCommandHandler handler,
        DbConnection connection,
        string id,
        string nodeId,
        string key,
        string value,
        int sortOrder)
    {
        await handler.Handle(
            new CreateVaultFieldCommand(connection, id, nodeId, key, Encoding.UTF8.GetBytes(value), sortOrder, CreatedAtUtc, CreatedAtUtc),
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

        public SqliteVaultFieldWriter FieldStore { get; } = new();

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
                ListVaultFieldsByNodeIdQuery query => await new ListVaultFieldsByNodeIdQueryHandler(FieldStore)
                    .Handle(query, cancellationToken)
                    .ConfigureAwait(false),
                ReorderVaultFieldCommand command => await new ReorderVaultFieldCommandHandler(FieldStore)
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
                case ReorderVaultFieldCommand command:
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
}
