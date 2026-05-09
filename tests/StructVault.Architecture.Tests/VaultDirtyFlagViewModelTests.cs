using MediatR;
using StructVault.Application.Persistence;
using StructVault.Application.Errors;
using StructVault.Application.Qps;
using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultDirtyFlagViewModelTests
{
    [Fact]
    public async Task LoadingAndSelectingVaultContentKeepsDirtyFlagClean()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        await sender.Send(
            new CreateVaultNodeCommand(connection, "root", null, "Root", 0, timestamp, timestamp),
            CancellationToken.None);
        MainWindowViewModel viewModel = new(sender, new RecordingContextMenuInputService());

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "correct horse battery staple");
        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));

        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task SuccessfulMutationSetsDirtyFlagAndSuccessfulSaveClearsIt()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new() { NextNodeName = "  Root  " };
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "correct horse battery staple");

        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();

        Assert.True(viewModel.IsDirty);
        Assert.Single(sender.HandledRequests.OfType<CreateVaultNodeCommand>());

        await viewModel.SaveVaultAsync();

        Assert.False(viewModel.IsDirty);
        TrySaveQpsVaultFileCommand saveCommand = Assert.Single(sender.HandledRequests.OfType<TrySaveQpsVaultFileCommand>());
        Assert.Same(connection, saveCommand.Connection);
        Assert.Equal("vault.qps", saveCommand.FilePath);
        Assert.Equal("correct horse battery staple", saveCommand.Password);
    }

    [Fact]
    public async Task SuccessfulFieldMutationSetsDirtyFlag()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new() { NextNodeName = "Root" };
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "correct horse battery staple");
        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();
        await viewModel.SaveVaultAsync();
        inputService.NextFieldInput = new VaultFieldInput("  Username  ", "  admin  ");

        await ((AsyncCommand)viewModel.AddFieldCommand).ExecuteAsync(Assert.Single(viewModel.VaultNodes));

        Assert.True(viewModel.IsDirty);
        CreateVaultFieldCommand command = Assert.Single(sender.HandledRequests.OfType<CreateVaultFieldCommand>());
        Assert.Equal("Username", command.Key);
    }

    [Fact]
    public async Task CancelledOrInvalidMutationDoesNotSetDirtyFlag()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new() { NextNodeName = "   " };
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "correct horse battery staple");

        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();

        Assert.False(viewModel.IsDirty);
        Assert.Empty(sender.HandledRequests.OfType<CreateVaultNodeCommand>());
        Assert.Equal("Node name", inputService.LastValidationTitle);
        Assert.Equal("Node names cannot be empty.", inputService.LastValidationMessage);
    }

    [Fact]
    public async Task MutationThatDoesNotUpdateARecordDoesNotSetDirtyFlag()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new();
        RecordingContextMenuInputService inputService = new() { NextNodeName = "Missing" };
        MainWindowViewModel viewModel = new(sender, inputService);
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        VaultNodeHierarchyRecord missingNode = new("missing", null, "Original", 0, timestamp, timestamp, Array.Empty<VaultNodeHierarchyRecord>());
        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "correct horse battery staple");

        await ((AsyncCommand)viewModel.RenameNodeCommand).ExecuteAsync(new VaultTreeNodeViewModel(missingNode));

        Assert.False(viewModel.IsDirty);
        Assert.Single(sender.HandledRequests.OfType<UpdateVaultNodeCommand>());
    }

    [Fact]
    public async Task FailedSavePreservesDirtyFlag()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        PersistenceBackedSender sender = new() { ThrowOnSave = true };
        RecordingContextMenuInputService inputService = new() { NextNodeName = "Root" };
        MainWindowViewModel viewModel = new(sender, inputService);
        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "correct horse battery staple");
        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();

        await viewModel.SaveVaultAsync();

        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.HasVaultError);
        Assert.Contains("configured test save operation failed", viewModel.VaultErrorMessage, StringComparison.Ordinal);
        Assert.Single(sender.HandledRequests.OfType<TrySaveQpsVaultFileCommand>());
    }

    private sealed class RecordingContextMenuInputService : IContextMenuInputService
    {
        public string? NextNodeName { get; set; }

        public VaultFieldInput? NextFieldInput { get; set; }

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
            return true;
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

    private sealed class PersistenceBackedSender : ISender
    {
        private readonly SqliteVaultNodeWriter nodeWriter = new();
        private readonly SqliteVaultFieldWriter fieldWriter = new();

        public List<object> HandledRequests { get; } = new();

        public bool ThrowOnSave { get; init; }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            HandledRequests.Add(request);

            object? response = request switch
            {
                ListVaultNodeHierarchyQuery query => await new ListVaultNodeHierarchyQueryHandler(nodeWriter)
                    .Handle(query, cancellationToken)
                    .ConfigureAwait(false),
                ListVaultFieldsByNodeIdQuery query => await new ListVaultFieldsByNodeIdQueryHandler(fieldWriter)
                    .Handle(query, cancellationToken)
                    .ConfigureAwait(false),
                UpdateVaultNodeCommand command => await new UpdateVaultNodeCommandHandler(nodeWriter)
                    .Handle(command, cancellationToken)
                    .ConfigureAwait(false),
                UpdateVaultFieldCommand command => await new UpdateVaultFieldCommandHandler(fieldWriter)
                    .Handle(command, cancellationToken)
                    .ConfigureAwait(false),
                TrySaveQpsVaultFileCommand => ThrowOnSave
                    ? VaultOperationResult.Failure(new VaultOperationError(
                        VaultOperationErrorCode.FileAccessFailed,
                        "The configured test save operation failed."))
                    : VaultOperationResult.Success(),
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
                    await new CreateVaultNodeCommandHandler(nodeWriter).Handle(command, cancellationToken).ConfigureAwait(false);
                    break;
                case DeleteVaultNodeCommand command:
                    await new DeleteVaultNodeCommandHandler(nodeWriter).Handle(command, cancellationToken).ConfigureAwait(false);
                    break;
                case CreateVaultFieldCommand command:
                    await new CreateVaultFieldCommandHandler(fieldWriter).Handle(command, cancellationToken).ConfigureAwait(false);
                    break;
                case DeleteVaultFieldCommand command:
                    await new DeleteVaultFieldCommandHandler(fieldWriter).Handle(command, cancellationToken).ConfigureAwait(false);
                    break;
                case SaveQpsVaultFileCommand:
                    if (ThrowOnSave)
                    {
                        throw new InvalidOperationException("The configured test save operation failed.");
                    }

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
                case UpdateVaultNodeCommand command:
                    return await Send<bool>(command, cancellationToken).ConfigureAwait(false);
                case UpdateVaultFieldCommand command:
                    return await Send<bool>(command, cancellationToken).ConfigureAwait(false);
                case CreateVaultNodeCommand command:
                    await Send(command, cancellationToken).ConfigureAwait(false);
                    return null;
                case DeleteVaultNodeCommand command:
                    await Send(command, cancellationToken).ConfigureAwait(false);
                    return null;
                case CreateVaultFieldCommand command:
                    await Send(command, cancellationToken).ConfigureAwait(false);
                    return null;
                case DeleteVaultFieldCommand command:
                    await Send(command, cancellationToken).ConfigureAwait(false);
                    return null;
                case TrySaveQpsVaultFileCommand command:
                    return await Send<VaultOperationResult>(command, cancellationToken).ConfigureAwait(false);
                default:
                    throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.");
            }
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Dirty flag tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Dirty flag tests do not use streaming requests.");
        }
    }
}
