namespace StructVault.Application.Persistence;

public static class VaultSettingNames
{
    public const string ClipboardAutoClearEnabled = "Clipboard.AutoClear.Enabled";
    public const string ClipboardAutoClearDelaySeconds = "Clipboard.AutoClear.DelaySeconds";

    public static bool IsSupported(string name)
    {
        return string.Equals(name, ClipboardAutoClearEnabled, StringComparison.Ordinal)
            || string.Equals(name, ClipboardAutoClearDelaySeconds, StringComparison.Ordinal);
    }
}
