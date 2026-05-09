using System.Data.Common;
using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.IdleLock;
using StructVault.Application.Persistence;
using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultIdleLockSettingsTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 5, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetIdleLockSettingsQueryHandlerReturnsSecureDefaultsWhenSettingsAreMissing()
    {
        await using DbConnection connection = await CreateVaultConnectionAsync();
        SqliteVaultSettingStore settingStore = new();
        GetIdleLockSettingsQueryHandler handler = new(settingStore);

        IdleLockSettingsRecord settings = await handler.Handle(new GetIdleLockSettingsQuery(connection), CancellationToken.None);

        Assert.True(settings.IsEnabled);
        Assert.Equal(TimeSpan.FromMinutes(15), settings.Timeout);
    }

    [Fact]
    public async Task SaveIdleLockSettingsCommandHandlerPersistsSettingsInVaultDatabase()
    {
        await using DbConnection connection = await CreateVaultConnectionAsync();
        SqliteVaultSettingStore settingStore = new();
        SaveIdleLockSettingsCommandHandler saveHandler = new(settingStore);
        GetIdleLockSettingsQueryHandler getHandler = new(settingStore);

        await saveHandler.Handle(new SaveIdleLockSettingsCommand(connection, isEnabled: false, TimeSpan.FromSeconds(300)), CancellationToken.None);
        IdleLockSettingsRecord settings = await getHandler.Handle(new GetIdleLockSettingsQuery(connection), CancellationToken.None);

        Assert.False(settings.IsEnabled);
        Assert.Equal(TimeSpan.FromSeconds(300), settings.Timeout);
    }

    [Fact]
    public async Task IdleLockSettingsSurviveVaultDatabaseSerializationRoundTrip()
    {
        await using DbConnection sourceConnection = await CreateVaultConnectionAsync();
        SqliteVaultSettingStore settingStore = new();
        SaveIdleLockSettingsCommandHandler saveHandler = new(settingStore);
        GetIdleLockSettingsQueryHandler getHandler = new(settingStore);
        SqliteVaultDatabaseSerializer serializer = new(new SqliteVaultSchemaProvider());

        await saveHandler.Handle(new SaveIdleLockSettingsCommand(sourceConnection, isEnabled: false, TimeSpan.FromSeconds(600)), CancellationToken.None);
        byte[] databaseImage = await serializer.SerializeAsync(sourceConnection, CancellationToken.None);

        await using DbConnection restoredConnection = await serializer.DeserializeAsync(databaseImage, CancellationToken.None);
        IdleLockSettingsRecord settings = await getHandler.Handle(new GetIdleLockSettingsQuery(restoredConnection), CancellationToken.None);

        Assert.False(settings.IsEnabled);
        Assert.Equal(TimeSpan.FromSeconds(600), settings.Timeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SaveIdleLockSettingsCommandRejectsInvalidTimeout(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaveIdleLockSettingsCommand(new SqliteConnection(), isEnabled: true, TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public async Task MainWindowViewModelLoadsIdleLockSettingsAndAppliesTimeoutAtRuntime()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, Timestamp, Timestamp, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new([root])
        {
            IdleLockSettings = new IdleLockSettingsRecord(true, TimeSpan.FromSeconds(120)),
            LockState = new VaultLockState(true, Timestamp, Timestamp.AddSeconds(120), TimeSpan.FromSeconds(120))
        };
        MainWindowViewModel viewModel = new(sender);
        await using DbConnection connection = await CreateVaultConnectionAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "master-password");
        bool locked = await viewModel.LockVaultAfterIdleTimeoutAsync();

        Assert.True(locked);
        LockVaultAfterIdleTimeoutCommand command = Assert.Single(sender.Requests.OfType<LockVaultAfterIdleTimeoutCommand>());
        Assert.Equal(TimeSpan.FromSeconds(120), command.IdleTimeout);
    }

    [Fact]
    public async Task MainWindowViewModelDoesNotLockAtRuntimeWhenIdleLockSettingIsDisabled()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, Timestamp, Timestamp, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new([root])
        {
            IdleLockSettings = new IdleLockSettingsRecord(false, TimeSpan.FromSeconds(60)),
            LockState = new VaultLockState(true, Timestamp, Timestamp.AddSeconds(60), TimeSpan.FromSeconds(60))
        };
        MainWindowViewModel viewModel = new(sender);
        await using DbConnection connection = await CreateVaultConnectionAsync();

        await viewModel.LoadVaultTreeAsync(connection, "vault.qps", "master-password");
        bool locked = await viewModel.LockVaultAfterIdleTimeoutAsync();

        Assert.False(locked);
        Assert.False(viewModel.IsVaultLocked);
        Assert.Empty(sender.Requests.OfType<LockVaultAfterIdleTimeoutCommand>());
    }

    [Fact]
    public async Task MainWindowViewModelSavesIdleLockSettingsThroughCommandAndMarksVaultDirty()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, Timestamp, Timestamp, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new([root]);
        MainWindowViewModel viewModel = new(sender);
        await using DbConnection connection = await CreateVaultConnectionAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        viewModel.IsIdleLockEnabled = false;
        viewModel.IdleLockTimeoutSeconds = 180;
        await ((AsyncCommand)viewModel.ApplyIdleLockSettingsCommand).ExecuteAsync(null);

        SaveIdleLockSettingsCommand command = Assert.IsType<SaveIdleLockSettingsCommand>(sender.LastRequest);
        Assert.False(command.IsEnabled);
        Assert.Equal(TimeSpan.FromSeconds(180), command.Timeout);
        Assert.True(viewModel.IsDirty);
    }

    private static async Task<DbConnection> CreateVaultConnectionAsync()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        return await factory.CreateOpenConnectionAsync(CancellationToken.None);
    }

    private sealed class RecordingSender : ISender
    {
        public RecordingSender(IReadOnlyList<VaultNodeHierarchyRecord> hierarchy)
        {
            Hierarchy = hierarchy;
        }

        public IReadOnlyList<VaultNodeHierarchyRecord> Hierarchy { get; }

        public ClipboardSettingsRecord ClipboardSettings { get; init; } = ClipboardSettingsRecord.Default;

        public IdleLockSettingsRecord IdleLockSettings { get; init; } = IdleLockSettingsRecord.Default;

        public VaultLockState LockState { get; init; } = new(false, Timestamp, Timestamp, TimeSpan.Zero);

        public List<object> Requests { get; } = new();

        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            LastRequest = request;

            object response = request switch
            {
                GetClipboardSettingsQuery => ClipboardSettings,
                GetIdleLockSettingsQuery => IdleLockSettings,
                ListVaultNodeHierarchyQuery => Hierarchy,
                LockVaultAfterIdleTimeoutCommand => LockState,
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };

            return Task.FromResult((TResponse)response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            LastRequest = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            LastRequest = request;
            return request switch
            {
                GetClipboardSettingsQuery => Task.FromResult<object?>(ClipboardSettings),
                GetIdleLockSettingsQuery => Task.FromResult<object?>(IdleLockSettings),
                ListVaultNodeHierarchyQuery => Task.FromResult<object?>(Hierarchy),
                LockVaultAfterIdleTimeoutCommand => Task.FromResult<object?>(LockState),
                SaveIdleLockSettingsCommand => Task.FromResult<object?>(null),
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Idle lock settings tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Idle lock settings tests do not use streaming requests.");
        }
    }
}
