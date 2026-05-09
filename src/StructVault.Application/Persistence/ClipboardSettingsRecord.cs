namespace StructVault.Application.Persistence;

public sealed record ClipboardSettingsRecord(bool AutoClearEnabled, TimeSpan AutoClearDelay)
{
    public static ClipboardSettingsRecord Default { get; } = new(true, TimeSpan.FromSeconds(30));
}
