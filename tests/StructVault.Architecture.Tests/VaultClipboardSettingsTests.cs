using System.Data.Common;
using MediatR;
using Microsoft.Data.Sqlite;
using StructVault.Application.Clipboard;
using StructVault.Application.Persistence;
using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultClipboardSettingsTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 5, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetClipboardSettingsQueryHandlerReturnsSecureDefaultsWhenSettingsAreMissing()
    {
        await using DbConnection connection = await CreateVaultConnectionAsync();
        SqliteVaultSettingStore settingStore = new();
        GetClipboardSettingsQueryHandler handler = new(settingStore);

        ClipboardSettingsRecord settings = await handler.Handle(new GetClipboardSettingsQuery(connection), CancellationToken.None);

        Assert.True(settings.AutoClearEnabled);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.AutoClearDelay);
    }

    [Fact]
    public async Task SaveClipboardSettingsCommandHandlerPersistsSettingsInVaultDatabase()
    {
        await using DbConnection connection = await CreateVaultConnectionAsync();
        SqliteVaultSettingStore settingStore = new();
        SaveClipboardSettingsCommandHandler saveHandler = new(settingStore);
        GetClipboardSettingsQueryHandler getHandler = new(settingStore);

        await saveHandler.Handle(new SaveClipboardSettingsCommand(connection, autoClearEnabled: false, TimeSpan.FromSeconds(45)), CancellationToken.None);
        ClipboardSettingsRecord settings = await getHandler.Handle(new GetClipboardSettingsQuery(connection), CancellationToken.None);

        Assert.False(settings.AutoClearEnabled);
        Assert.Equal(TimeSpan.FromSeconds(45), settings.AutoClearDelay);
    }


    [Fact]
    public async Task ClipboardSettingsSurviveVaultDatabaseSerializationRoundTrip()
    {
        await using DbConnection sourceConnection = await CreateVaultConnectionAsync();
        SqliteVaultSettingStore settingStore = new();
        SaveClipboardSettingsCommandHandler saveHandler = new(settingStore);
        GetClipboardSettingsQueryHandler getHandler = new(settingStore);
        SqliteVaultDatabaseSerializer serializer = new(new SqliteVaultSchemaProvider());

        await saveHandler.Handle(new SaveClipboardSettingsCommand(sourceConnection, autoClearEnabled: false, TimeSpan.FromSeconds(120)), CancellationToken.None);
        byte[] databaseImage = await serializer.SerializeAsync(sourceConnection, CancellationToken.None);

        await using DbConnection restoredConnection = await serializer.DeserializeAsync(databaseImage, CancellationToken.None);
        ClipboardSettingsRecord settings = await getHandler.Handle(new GetClipboardSettingsQuery(restoredConnection), CancellationToken.None);

        Assert.False(settings.AutoClearEnabled);
        Assert.Equal(TimeSpan.FromSeconds(120), settings.AutoClearDelay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SaveClipboardSettingsCommandRejectsInvalidDelay(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaveClipboardSettingsCommand(new SqliteConnection(), autoClearEnabled: true, TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public async Task MainWindowViewModelLoadsClipboardSettingsAndAppliesThemToCopyCommand()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, Timestamp, Timestamp, Array.Empty<VaultNodeHierarchyRecord>());
        VaultFieldRecord field = new("field-secret", "node-root", "Secret", "value"u8.ToArray(), 0, Timestamp, Timestamp);
        RecordingSender sender = new([root])
        {
            ClipboardSettings = new ClipboardSettingsRecord(false, TimeSpan.FromSeconds(90)),
            FieldsByNodeId = { ["node-root"] = new[] { field } }
        };
        MainWindowViewModel viewModel = new(sender);
        await using DbConnection connection = await CreateVaultConnectionAsync();

        await viewModel.LoadVaultTreeAsync(connection);
        await viewModel.SelectVaultNodeAsync(Assert.Single(viewModel.VaultNodes));
        await ((AsyncCommand)viewModel.CopyFieldValueCommand).ExecuteAsync(Assert.Single(viewModel.SelectedFields));

        CopyVaultFieldValueToClipboardCommand command = Assert.IsType<CopyVaultFieldValueToClipboardCommand>(sender.LastRequest);
        Assert.False(command.AutoClearEnabled);
        Assert.Equal(TimeSpan.FromSeconds(90), command.AutoClearDelay);
    }

    [Fact]
    public async Task MainWindowViewModelSavesClipboardSettingsThroughCommandAndMarksVaultDirty()
    {
        VaultNodeHierarchyRecord root = new("node-root", null, "Root", 0, Timestamp, Timestamp, Array.Empty<VaultNodeHierarchyRecord>());
        RecordingSender sender = new([root]);
        MainWindowViewModel viewModel = new(sender);
        await using DbConnection connection = await CreateVaultConnectionAsync();
        await viewModel.LoadVaultTreeAsync(connection);

        viewModel.IsClipboardAutoClearEnabled = false;
        viewModel.ClipboardAutoClearDelaySeconds = 75;
        await ((AsyncCommand)viewModel.ApplyClipboardSettingsCommand).ExecuteAsync(null);

        SaveClipboardSettingsCommand command = Assert.IsType<SaveClipboardSettingsCommand>(sender.LastRequest);
        Assert.False(command.AutoClearEnabled);
        Assert.Equal(TimeSpan.FromSeconds(75), command.AutoClearDelay);
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

        public Dictionary<string, IReadOnlyList<VaultFieldRecord>> FieldsByNodeId { get; } = new(StringComparer.Ordinal);

        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (request is GetClipboardSettingsQuery && ClipboardSettings is TResponse settingsResponse)
            {
                return Task.FromResult(settingsResponse);
            }

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
            return request switch
            {
                GetClipboardSettingsQuery => Task.FromResult<object?>(ClipboardSettings),
                ListVaultNodeHierarchyQuery => Task.FromResult<object?>(Hierarchy),
                ListVaultFieldsByNodeIdQuery fieldQuery => Task.FromResult<object?>(FieldsByNodeId.GetValueOrDefault(fieldQuery.NodeId, Array.Empty<VaultFieldRecord>())),
                CopyVaultFieldValueToClipboardCommand => Task.FromResult<object?>(null),
                SaveClipboardSettingsCommand => Task.FromResult<object?>(null),
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Clipboard settings tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Clipboard settings tests do not use streaming requests.");
        }
    }
}
