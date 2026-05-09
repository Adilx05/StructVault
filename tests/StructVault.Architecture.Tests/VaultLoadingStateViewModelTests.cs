using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Errors;
using StructVault.Application.IdleLock;
using StructVault.Application.Persistence;
using StructVault.Application.Qps;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultLoadingStateViewModelTests
{
    [Fact]
    public async Task LoadVaultTreeExposesAndClearsLoadingState()
    {
        BlockingSender sender = new();
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        Task loadTask = viewModel.LoadVaultTreeAsync(connection);
        await sender.WaitForHierarchyRequestAsync();

        Assert.True(viewModel.IsLoading);
        Assert.Equal("Loading vault...", viewModel.LoadingStatusText);
        Assert.False(viewModel.AddRootNodeCommand.CanExecute(null));

        sender.CompleteHierarchy([]);
        await loadTask;

        Assert.False(viewModel.IsLoading);
        Assert.Equal("Ready.", viewModel.LoadingStatusText);
        Assert.True(viewModel.AddRootNodeCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadingStateClearsWhenMediatRRequestFails()
    {
        BlockingSender sender = new();
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        Task loadTask = viewModel.LoadVaultTreeAsync(connection);
        await sender.WaitForHierarchyRequestAsync();
        sender.FailHierarchy(new InvalidOperationException("The hierarchy could not be loaded."));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => loadTask);

        Assert.Contains("hierarchy", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.IsLoading);
        Assert.Equal("Ready.", viewModel.LoadingStatusText);
    }

    [Fact]
    public async Task SaveCommandIsDisabledWhileLoadIsInProgress()
    {
        BlockingSender sender = new();
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        Task loadTask = viewModel.LoadVaultTreeAsync(connection, "vault.qps", "master-password");
        await sender.WaitForHierarchyRequestAsync();

        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.SaveVaultCommand.CanExecute(null));

        sender.CompleteHierarchy([]);
        await loadTask;

        Assert.True(viewModel.CanSave);
        Assert.True(viewModel.SaveVaultCommand.CanExecute(null));
    }

    [Fact]
    public void MainWindowBindsLoadingOverlayToViewModelState()
    {
        string xaml = File.ReadAllText(GetRepositoryFile("src/StructVault.Desktop/MainWindow.xaml"));

        Assert.Contains("Visibility=\"{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsActive=\"{Binding IsLoading}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LoadingStatusText}\"", xaml, StringComparison.Ordinal);
    }

    private static string GetRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TASKS.md")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("Could not locate the repository root containing TASKS.md.");
        }

        return Path.Combine(directory.FullName, relativePath);
    }

    private sealed class BlockingSender : ISender
    {
        private readonly TaskCompletionSource<IReadOnlyList<VaultNodeHierarchyRecord>> hierarchyResponse =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource hierarchyRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForHierarchyRequestAsync()
        {
            return hierarchyRequested.Task;
        }

        public void CompleteHierarchy(IReadOnlyList<VaultNodeHierarchyRecord> hierarchy)
        {
            hierarchyResponse.TrySetResult(hierarchy);
        }

        public void FailHierarchy(Exception exception)
        {
            hierarchyResponse.TrySetException(exception);
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            object response = request switch
            {
                GetClipboardSettingsQuery => ClipboardSettingsRecord.Default,
                GetIdleLockSettingsQuery => IdleLockSettingsRecord.Default,
                TrySaveQpsVaultFileCommand => VaultOperationResult.Success(),
                LockVaultAfterIdleTimeoutCommand => new VaultLockState(false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero),
                GetIdleActivityStateQuery query => new IdleActivityState(DateTimeOffset.UtcNow, query.ObservedAtUtc ?? DateTimeOffset.UtcNow, TimeSpan.Zero, false),
                ListVaultFieldsByNodeIdQuery => Array.Empty<VaultFieldRecord>(),
                SearchVaultQuery => Array.Empty<VaultSearchResultRecord>(),
                ListVaultNodeHierarchyQuery => await GetHierarchyAsync(cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };

            return (TResponse)response;
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            return request switch
            {
                GetClipboardSettingsQuery => ClipboardSettingsRecord.Default,
                GetIdleLockSettingsQuery => IdleLockSettingsRecord.Default,
                TrySaveQpsVaultFileCommand => VaultOperationResult.Success(),
                LockVaultAfterIdleTimeoutCommand => new VaultLockState(false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, TimeSpan.Zero),
                GetIdleActivityStateQuery query => new IdleActivityState(DateTimeOffset.UtcNow, query.ObservedAtUtc ?? DateTimeOffset.UtcNow, TimeSpan.Zero, false),
                ListVaultFieldsByNodeIdQuery => Array.Empty<VaultFieldRecord>(),
                SearchVaultQuery => Array.Empty<VaultSearchResultRecord>(),
                ListVaultNodeHierarchyQuery => await GetHierarchyAsync(cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Loading state view model tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Loading state view model tests do not use streaming requests.");
        }

        private async Task<IReadOnlyList<VaultNodeHierarchyRecord>> GetHierarchyAsync(CancellationToken cancellationToken)
        {
            hierarchyRequested.TrySetResult();
            return await hierarchyResponse.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
