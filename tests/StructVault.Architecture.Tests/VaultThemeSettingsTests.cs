using System.Data.Common;
using System.IO;
using MediatR;
using StructVault.Application.Persistence;
using StructVault.Desktop.Commands;
using StructVault.Desktop.Services;
using StructVault.Desktop.ViewModels;
using StructVault.Persistence.Database;
using StructVault.Persistence.Schema;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class VaultThemeSettingsTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.Parse("2024-01-01T00:00:00Z");

    [Fact]
    public async Task GetThemeSettingsReturnsDefaultLightThemeWhenSettingIsMissing()
    {
        SqliteVaultSettingStore settingStore = new();
        await using DbConnection connection = await CreateVaultConnectionAsync();
        GetThemeSettingsQueryHandler handler = new(settingStore);

        ThemeSettingsRecord settings = await handler.Handle(new GetThemeSettingsQuery(connection), CancellationToken.None);

        Assert.Equal(ThemeSettingsRecord.LightBlueThemeName, settings.ThemeName);
    }

    [Fact]
    public async Task SaveThemeSettingsPersistsSupportedThemeName()
    {
        SqliteVaultSettingStore settingStore = new();
        await using DbConnection connection = await CreateVaultConnectionAsync();
        SaveThemeSettingsCommandHandler saveHandler = new(settingStore);
        GetThemeSettingsQueryHandler getHandler = new(settingStore);

        await saveHandler.Handle(new SaveThemeSettingsCommand(connection, ThemeSettingsRecord.LightPurpleThemeName), CancellationToken.None);
        ThemeSettingsRecord settings = await getHandler.Handle(new GetThemeSettingsQuery(connection), CancellationToken.None);

        Assert.Equal(ThemeSettingsRecord.LightPurpleThemeName, settings.ThemeName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Dark.Blue")]
    public async Task SaveThemeSettingsRejectsUnsupportedThemeNames(string themeName)
    {
        await using DbConnection connection = await CreateVaultConnectionAsync();

        Assert.ThrowsAny<ArgumentException>(() => new SaveThemeSettingsCommand(connection, themeName));
    }

    [Fact]
    public async Task GetThemeSettingsRejectsCorruptedThemeSetting()
    {
        SqliteVaultSettingStore settingStore = new();
        await using DbConnection connection = await CreateVaultConnectionAsync();
        await settingStore.UpsertManyAsync(
            connection,
            [new VaultSettingRecord(VaultSettingNames.ThemeName, "Dark.Orange")],
            CancellationToken.None);
        GetThemeSettingsQueryHandler handler = new(settingStore);

        await Assert.ThrowsAsync<InvalidDataException>(() => handler.Handle(new GetThemeSettingsQuery(connection), CancellationToken.None));
    }

    [Fact]
    public async Task MainWindowViewModelLoadsThemeSettingAndAppliesIt()
    {
        RecordingThemeService themeService = new();
        RecordingSender sender = new(CreateHierarchy());
        InMemoryApplicationSettingsService settingsService = new(new ApplicationSettings
        {
            ThemeName = ThemeSettingsRecord.LightPurpleThemeName
        });
        MainWindowViewModel viewModel = new(sender, new ContextMenuInputService(), new UiResponsivenessOptions(), themeService, settingsService);
        viewModel.LoadApplicationSettings();
        themeService.AppliedThemeNames.Clear();
        await using DbConnection connection = await CreateVaultConnectionAsync();

        await viewModel.LoadVaultTreeAsync(connection);

        Assert.Equal(ThemeSettingsRecord.LightPurpleThemeName, viewModel.SelectedThemeName);
        Assert.Equal(ThemeSettingsRecord.LightPurpleThemeName, Assert.Single(themeService.AppliedThemeNames));
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task MainWindowViewModelSavesThemeSettingsThroughCommandAsApplicationSetting()
    {
        RecordingThemeService themeService = new();
        RecordingSender sender = new(CreateHierarchy());
        InMemoryApplicationSettingsService settingsService = new();
        MainWindowViewModel viewModel = new(sender, new ContextMenuInputService(), new UiResponsivenessOptions(), themeService, settingsService);
        await using DbConnection connection = await CreateVaultConnectionAsync();
        await viewModel.LoadVaultTreeAsync(connection);
        themeService.AppliedThemeNames.Clear();

        viewModel.SelectedThemeName = ThemeSettingsRecord.LightPurpleThemeName;
        await ((AsyncCommand)viewModel.ApplyThemeSettingsCommand).ExecuteAsync(null);

        Assert.Equal(ThemeSettingsRecord.LightPurpleThemeName, settingsService.Settings.ThemeName);
        Assert.Equal(ThemeSettingsRecord.LightPurpleThemeName, Assert.Single(themeService.AppliedThemeNames));
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void MainWindowViewModelRejectsUnsupportedSelectedThemeNames()
    {
        RecordingSender sender = new(CreateHierarchy());
        MainWindowViewModel viewModel = new(sender, new ContextMenuInputService(), new UiResponsivenessOptions(), new RecordingThemeService());

        Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.SelectedThemeName = "Dark.Red");
    }

    private static IReadOnlyList<VaultNodeHierarchyRecord> CreateHierarchy()
    {
        return [new VaultNodeHierarchyRecord("node-root", null, "Root", 0, Timestamp, Timestamp, Array.Empty<VaultNodeHierarchyRecord>())];
    }

    private static async Task<DbConnection> CreateVaultConnectionAsync()
    {
        SqliteInMemoryVaultDatabaseConnectionFactory factory = new(new SqliteVaultSchemaProvider());
        return await factory.CreateOpenConnectionAsync(CancellationToken.None);
    }

    private sealed class RecordingThemeService : IThemeService
    {
        public List<string> AppliedThemeNames { get; } = new();

        public void ApplyTheme(string themeName)
        {
            AppliedThemeNames.Add(themeName);
        }
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

        public ThemeSettingsRecord ThemeSettings { get; init; } = ThemeSettingsRecord.Default;

        public object? LastRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;

            object response = request switch
            {
                GetClipboardSettingsQuery => ClipboardSettings,
                GetIdleLockSettingsQuery => IdleLockSettings,
                GetThemeSettingsQuery => ThemeSettings,
                ListVaultNodeHierarchyQuery => Hierarchy,
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };

            return Task.FromResult((TResponse)response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return request switch
            {
                GetClipboardSettingsQuery => Task.FromResult<object?>(ClipboardSettings),
                GetIdleLockSettingsQuery => Task.FromResult<object?>(IdleLockSettings),
                GetThemeSettingsQuery => Task.FromResult<object?>(ThemeSettings),
                ListVaultNodeHierarchyQuery => Task.FromResult<object?>(Hierarchy),
                SaveThemeSettingsCommand => Task.FromResult<object?>(null),
                _ => throw new InvalidOperationException($"Unexpected request type '{request.GetType().Name}'.")
            };
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Theme settings tests do not use streaming requests.");
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Theme settings tests do not use streaming requests.");
        }
    }
}
