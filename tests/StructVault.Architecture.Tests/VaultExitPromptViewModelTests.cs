using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Persistence;
using StructVault.Application.Qps;
using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultExitPromptViewModelTests
{
    [Fact]
    public async Task ConfirmExitReturnsTrueWithoutPromptWhenVaultIsClean()
    {
        RecordingContextMenuInputService inputService = new();
        PersistenceBackedSender sender = new();
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = await OpenVaultConnectionAsync();

        await viewModel.LoadVaultTreeAsync(connection);

        bool shouldExit = await viewModel.ConfirmExitAsync();

        Assert.True(shouldExit);
        Assert.Equal(0, inputService.PromptCount);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task ConfirmExitCancelsWhenDirtyVaultPromptIsCancelled()
    {
        RecordingContextMenuInputService inputService = new()
        {
            NextNodeName = "Root",
            ExitChoice = UnsavedChangesExitChoice.CancelExit
        };
        PersistenceBackedSender sender = new();
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = await OpenVaultConnectionAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "save-password");
        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();

        bool shouldExit = await viewModel.ConfirmExitAsync();

        Assert.False(shouldExit);
        Assert.True(viewModel.IsDirty);
        Assert.Single(inputService.PromptCanSaveValues, value => value);
        Assert.Empty(sender.HandledRequests.OfType<SaveQpsVaultFileCommand>());
    }

    [Fact]
    public async Task ConfirmExitAllowsDiscardWhenDirtyVaultHasNoSaveTarget()
    {
        RecordingContextMenuInputService inputService = new()
        {
            NextNodeName = "Root",
            ExitChoice = UnsavedChangesExitChoice.ExitWithoutSaving
        };
        PersistenceBackedSender sender = new();
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = await OpenVaultConnectionAsync();

        await viewModel.LoadVaultTreeAsync(connection);
        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();

        bool shouldExit = await viewModel.ConfirmExitAsync();

        Assert.True(shouldExit);
        Assert.True(viewModel.IsDirty);
        Assert.Single(inputService.PromptCanSaveValues, value => !value);
        Assert.Empty(sender.HandledRequests.OfType<SaveQpsVaultFileCommand>());
    }

    [Fact]
    public async Task ConfirmExitSavesDirtyVaultWhenPromptRequestsSave()
    {
        RecordingContextMenuInputService inputService = new()
        {
            NextNodeName = "Root",
            ExitChoice = UnsavedChangesExitChoice.SaveAndExit
        };
        PersistenceBackedSender sender = new();
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = await OpenVaultConnectionAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "save-password");
        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();

        bool shouldExit = await viewModel.ConfirmExitAsync();

        Assert.True(shouldExit);
        Assert.False(viewModel.IsDirty);
        SaveQpsVaultFileCommand command = Assert.Single(sender.HandledRequests.OfType<SaveQpsVaultFileCommand>());
        Assert.Same(connection, command.Connection);
        Assert.Equal("vault.qps", command.FilePath);
        Assert.Equal("save-password", command.Password);
    }

    [Fact]
    public async Task ConfirmExitBlocksExitAndPreservesDirtyStateWhenSaveFails()
    {
        RecordingContextMenuInputService inputService = new()
        {
            NextNodeName = "Root",
            ExitChoice = UnsavedChangesExitChoice.SaveAndExit
        };
        PersistenceBackedSender sender = new() { ThrowOnSave = true };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = await OpenVaultConnectionAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "save-password");
        await ((AsyncCommand)viewModel.AddRootNodeCommand).ExecuteAsync();

        bool shouldExit = await viewModel.ConfirmExitAsync();

        Assert.False(shouldExit);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("Save failed", inputService.LastValidationTitle);
        Assert.Single(sender.HandledRequests.OfType<SaveQpsVaultFileCommand>());
    }

    private static async Task<SqliteConnection> OpenVaultConnectionAsync()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory connectionFactory = new(new SqliteVaultSchemaProvider());
        return (SqliteConnection)await connectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
    }

    private sealed class RecordingContextMenuInputService : IContextMenuInputService
    {
        public string? NextNodeName { get; set; }

        public VaultFieldInput? NextFieldInput { get; set; }

        public UnsavedChangesExitChoice ExitChoice { get; set; } = UnsavedChangesExitChoice.CancelExit;

        public List<bool> PromptCanSaveValues { get; } = new();

        public int PromptCount => PromptCanSaveValues.Count;

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
            PromptCanSaveValues.Add(canSave);
            return ExitChoice;
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
                case CreateVaultNodeCommand command:
                    await Send(command, cancellationToken).ConfigureAwait(false);
                    return null;
                case SaveQpsVaultFileCommand command:
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
            throw new NotSupportedException("Exit prompt view model tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Exit prompt view model tests do not use streaming requests.");
        }
    }
}
