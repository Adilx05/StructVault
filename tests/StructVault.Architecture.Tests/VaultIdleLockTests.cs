using System.Text;
using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Abstractions.IdleLock;
using StructVault.Application.IdleLock;
using StructVault.Application.Persistence;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultIdleLockTests
{
    private static readonly DateTimeOffset InitialUtc = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LockVaultAfterIdleTimeoutCommandHandlerLocksWhenTimeoutIsReached()
    {
        ManualTimeProvider timeProvider = new(InitialUtc);
        IIdleActivityTracker tracker = new IdleActivityTracker(timeProvider);
        LockVaultAfterIdleTimeoutCommandHandler handler = new(tracker);

        VaultLockState state = await handler.Handle(
            new LockVaultAfterIdleTimeoutCommand(TimeSpan.FromMinutes(5), InitialUtc.AddMinutes(5)),
            CancellationToken.None);

        Assert.True(state.IsLocked);
        Assert.Equal(InitialUtc, state.LastActivityUtc);
        Assert.Equal(InitialUtc.AddMinutes(5), state.ObservedAtUtc);
        Assert.Equal(TimeSpan.FromMinutes(5), state.IdleDuration);
    }

    [Fact]
    public async Task LockVaultAfterIdleTimeoutCommandHandlerLeavesVaultUnlockedBeforeTimeout()
    {
        ManualTimeProvider timeProvider = new(InitialUtc);
        IIdleActivityTracker tracker = new IdleActivityTracker(timeProvider);
        LockVaultAfterIdleTimeoutCommandHandler handler = new(tracker);

        VaultLockState state = await handler.Handle(
            new LockVaultAfterIdleTimeoutCommand(TimeSpan.FromMinutes(5), InitialUtc.AddMinutes(4)),
            CancellationToken.None);

        Assert.False(state.IsLocked);
        Assert.Equal(TimeSpan.FromMinutes(4), state.IdleDuration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LockVaultAfterIdleTimeoutCommandRejectsNonPositiveTimeout(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockVaultAfterIdleTimeoutCommand(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MainWindowViewModelRejectsNonPositiveIdleLockTimeout(int seconds)
    {
        MainWindowViewModel viewModel = new(new RecordingSender(new VaultLockState(false, InitialUtc, InitialUtc, TimeSpan.Zero)));

        Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.IdleLockTimeout = TimeSpan.FromSeconds(seconds));
    }

    [Fact]
    public async Task MainWindowViewModelLocksAndClearsVaultPresentationAfterIdleTimeout()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, InitialUtc, InitialUtc, Array.Empty<VaultNodeHierarchyRecord>());
        VaultFieldRecord field = new("field-password", "node-root", "Password", Encoding.UTF8.GetBytes("sensitive-value"), 0, InitialUtc, InitialUtc);
        RecordingSender sender = new(new VaultLockState(true, InitialUtc, InitialUtc.AddMinutes(15), TimeSpan.FromMinutes(15)), [root])
        {
            FieldsByNodeId = { ["node-root"] = new[] { field } }
        };
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "master-password");
        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));

        Assert.False(viewModel.IsVaultLocked);
        Assert.True(viewModel.CanSave);
        Assert.Single(viewModel.SelectedFields);

        bool locked = await viewModel.LockVaultAfterIdleTimeoutAsync();

        Assert.True(locked);
        Assert.True(viewModel.IsVaultLocked);
        Assert.Empty(viewModel.VaultNodes);
        Assert.Empty(viewModel.SelectedFields);
        Assert.Empty(viewModel.SearchResults);
        Assert.False(viewModel.HasSelectedNode);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.AddRootNodeCommand.CanExecute(null));
        LockVaultAfterIdleTimeoutCommand command = Assert.Single(sender.Requests.OfType<LockVaultAfterIdleTimeoutCommand>());
        Assert.Equal(viewModel.IdleLockTimeout, command.IdleTimeout);
    }

    [Fact]
    public async Task MainWindowViewModelDoesNotLockBeforeIdleTimeout()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, InitialUtc, InitialUtc, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new(new VaultLockState(false, InitialUtc, InitialUtc.AddMinutes(1), TimeSpan.FromMinutes(1)), [root]);
        MainWindowViewModel viewModel = new(sender);
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        await viewModel.LoadVaultTreeAsync(connection);

        bool locked = await viewModel.LockVaultAfterIdleTimeoutAsync();

        Assert.False(locked);
        Assert.False(viewModel.IsVaultLocked);
        Assert.Single(viewModel.VaultNodes);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow.ToUniversalTime();
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingSender : ISender
    {
        private readonly IReadOnlyList<VaultNodeHierarchyRecord> hierarchy;
        private readonly VaultLockState lockState;

        public RecordingSender(VaultLockState lockState, IReadOnlyList<VaultNodeHierarchyRecord>? hierarchy = null)
        {
            this.lockState = lockState;
            this.hierarchy = hierarchy ?? Array.Empty<VaultNodeHierarchyRecord>();
        }

        public List<object> Requests { get; } = new();

        public Dictionary<string, IReadOnlyList<VaultFieldRecord>> FieldsByNodeId { get; } = new(StringComparer.Ordinal);

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);

            object response = request switch
            {
                ListVaultNodeHierarchyQuery => hierarchy,
                ListVaultFieldsByNodeIdQuery query => FieldsByNodeId.TryGetValue(query.NodeId, out IReadOnlyList<VaultFieldRecord>? fields)
                    ? fields
                    : Array.Empty<VaultFieldRecord>(),
                LockVaultAfterIdleTimeoutCommand => lockState,
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };

            return Task.FromResult((TResponse)response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Idle lock tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Idle lock tests do not use streaming requests.");
        }
    }
}
