using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Persistence;
using StructVault.Application.Errors;
using StructVault.Application.Qps;
using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultManualSaveViewModelTests
{
    [Fact]
    public async Task SaveCommandDispatchesManualSaveCommandWhenTargetIsConfigured()
    {
        RecordingSender sender = new();
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "save-password");

        Assert.True(viewModel.CanSave);
        Assert.True(viewModel.SaveVaultCommand.CanExecute(null));

        await ((AsyncCommand)viewModel.SaveVaultCommand).ExecuteAsync();

        TrySaveQpsVaultFileCommand command = Assert.Single(sender.Requests.OfType<TrySaveQpsVaultFileCommand>());
        Assert.Same(connection, command.Connection);
        Assert.Equal("vault.qps", command.FilePath);
        Assert.Equal("save-password", command.Password);
    }

    [Fact]
    public async Task SaveCommandPromptsForManualSaveTargetWhenNoneIsConfigured()
    {
        RecordingSender sender = new();
        RecordingContextMenuInputService inputService = new()
        {
            SaveTarget = new VaultSaveTargetInput("vault.qps", "save-password")
        };
        MainWindowViewModel viewModel = new(sender, inputService);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        await viewModel.LoadVaultTreeAsync(connection);

        Assert.True(viewModel.CanSave);
        Assert.True(viewModel.SaveVaultCommand.CanExecute(null));

        await ((AsyncCommand)viewModel.SaveVaultCommand).ExecuteAsync();

        TrySaveQpsVaultFileCommand command = Assert.Single(sender.Requests.OfType<TrySaveQpsVaultFileCommand>());
        Assert.Same(connection, command.Connection);
        Assert.Equal("vault.qps", command.FilePath);
        Assert.Equal("save-password", command.Password);
    }


    [Fact]
    public async Task SaveCommandShowsVaultErrorWhenSafeSaveFails()
    {
        RecordingSender sender = new()
        {
            SaveResult = VaultOperationResult.Failure(new VaultOperationError(
                VaultOperationErrorCode.FileAccessFailed,
                "The vault could not be saved because the vault file could not be written."))
        };
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "save-password");

        await ((AsyncCommand)viewModel.SaveVaultCommand).ExecuteAsync();

        Assert.True(viewModel.HasVaultError);
        Assert.Contains("could not be saved", viewModel.VaultErrorMessage, StringComparison.Ordinal);
        Assert.Single(sender.Requests.OfType<TrySaveQpsVaultFileCommand>());
    }

    [Fact]
    public void ConfigureManualSaveTargetRejectsBlankPassword()
    {
        MainWindowViewModel viewModel = new(new RecordingSender());

        Assert.Throws<ArgumentException>(() => viewModel.ConfigureManualSaveTarget("vault.qps", " "));
    }

    private sealed class RecordingContextMenuInputService : IContextMenuInputService
    {
        public VaultSaveTargetInput? SaveTarget { get; init; }

        public string? RequestNodeName(string title, string message, string? initialName = null) => null;

        public VaultFieldInput? RequestField(string title, string keyMessage, string valueMessage, VaultFieldInput? initialValue = null) => null;

        public string? RequestPassword(string title, string message) => null;

        public VaultSaveTargetInput? RequestSaveTarget(string title, string message) => SaveTarget;

        public bool ConfirmDelete(string title, string message) => false;

        public UnsavedChangesExitChoice PromptUnsavedChangesOnExit(bool canSave) => UnsavedChangesExitChoice.CancelExit;

        public void ShowValidationError(string title, string message)
        {
        }
    }

    private sealed class RecordingSender : ISender
    {
        public List<object> Requests { get; } = new();

        public VaultOperationResult SaveResult { get; init; } = VaultOperationResult.Success();

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            object? response = request switch
            {
                ListVaultNodeHierarchyQuery => Array.Empty<VaultNodeHierarchyRecord>(),
                TrySaveQpsVaultFileCommand => SaveResult,
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };

            return Task.FromResult((TResponse)response);
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
                return Task.FromResult<object?>(Array.Empty<VaultNodeHierarchyRecord>());
            }

            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Manual save view model tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Manual save view model tests do not use streaming requests.");
        }
    }
}
