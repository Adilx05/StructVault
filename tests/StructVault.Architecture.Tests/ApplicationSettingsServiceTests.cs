using StructVault.Application.Clipboard;
using StructVault.Application.IdleLock;
using StructVault.Application.Persistence;
using StructVault.Desktop.Services;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class ApplicationSettingsServiceTests
{
    [Fact]
    public void FileSystemApplicationSettingsServicePersistsNonSensitiveAppSettings()
    {
        string settingsPath = Path.Combine(Path.GetTempPath(), "StructVault.Tests", Guid.NewGuid().ToString("N"), "settings.xml");
        FileSystemApplicationSettingsService service = new(settingsPath);
        ApplicationSettings settings = new()
        {
            LastVaultFilePath = Path.Combine(Path.GetTempPath(), "workspace.qps"),
            ThemeName = ThemeSettingsRecord.LightPurpleThemeName,
            ClipboardAutoClearEnabled = false,
            ClipboardAutoClearDelaySeconds = 45,
            IdleLockEnabled = false,
            IdleLockTimeoutSeconds = 600
        };

        service.Save(settings);
        ApplicationSettings reloaded = service.Load();

        Assert.Equal(settings.LastVaultFilePath, reloaded.LastVaultFilePath);
        Assert.Equal(ThemeSettingsRecord.LightPurpleThemeName, reloaded.ThemeName);
        Assert.False(reloaded.ClipboardAutoClearEnabled);
        Assert.Equal(45, reloaded.ClipboardAutoClearDelaySeconds);
        Assert.False(reloaded.IdleLockEnabled);
        Assert.Equal(600, reloaded.IdleLockTimeoutSeconds);
        Assert.DoesNotContain("password", File.ReadAllText(settingsPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplicationSettingsNormalizeInvalidDurationsToSecureDefaults()
    {
        ApplicationSettings settings = new()
        {
            ThemeName = ThemeSettingsRecord.LightEmeraldThemeName,
            ClipboardAutoClearDelaySeconds = 0,
            IdleLockTimeoutSeconds = -1
        };

        ApplicationSettings normalized = settings.Normalize();

        Assert.Equal((int)ClipboardSettingsRecord.Default.AutoClearDelay.TotalSeconds, normalized.ClipboardAutoClearDelaySeconds);
        Assert.Equal((int)IdleLockSettingsRecord.Default.Timeout.TotalSeconds, normalized.IdleLockTimeoutSeconds);
    }
}
