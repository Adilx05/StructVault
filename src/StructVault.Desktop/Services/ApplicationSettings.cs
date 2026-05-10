using StructVault.Application.Clipboard;
using StructVault.Application.IdleLock;
using StructVault.Application.Persistence;

namespace StructVault.Desktop.Services;

public sealed class ApplicationSettings
{
    public string? LastVaultFilePath { get; init; }

    public string ThemeName { get; init; } = ThemeSettingsRecord.Default.ThemeName;

    public bool ClipboardAutoClearEnabled { get; init; } = ClipboardSettingsRecord.Default.AutoClearEnabled;

    public int ClipboardAutoClearDelaySeconds { get; init; } = (int)ClipboardSettingsRecord.Default.AutoClearDelay.TotalSeconds;

    public bool IdleLockEnabled { get; init; } = IdleLockSettingsRecord.Default.IsEnabled;

    public int IdleLockTimeoutSeconds { get; init; } = (int)IdleLockSettingsRecord.Default.Timeout.TotalSeconds;

    public bool MinimizeToTrayOnClose { get; init; } = true;

    public static ApplicationSettings Default { get; } = new();

    public ApplicationSettings Normalize()
    {
        return new ApplicationSettings
        {
            LastVaultFilePath = string.IsNullOrWhiteSpace(LastVaultFilePath) ? null : LastVaultFilePath.Trim(),
            ThemeName = ThemeSettingsRecord.NormalizeThemeName(ThemeName),
            ClipboardAutoClearEnabled = ClipboardAutoClearEnabled,
            ClipboardAutoClearDelaySeconds = ClipboardAutoClearDelaySeconds <= 0
                ? (int)ClipboardSettingsRecord.Default.AutoClearDelay.TotalSeconds
                : ClipboardAutoClearDelaySeconds,
            IdleLockEnabled = IdleLockEnabled,
            IdleLockTimeoutSeconds = IdleLockTimeoutSeconds <= 0
                ? (int)IdleLockSettingsRecord.Default.Timeout.TotalSeconds
                : IdleLockTimeoutSeconds,
            MinimizeToTrayOnClose = MinimizeToTrayOnClose
        };
    }
}
