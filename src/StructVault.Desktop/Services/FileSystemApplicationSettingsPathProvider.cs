using System.IO;

namespace StructVault.Desktop.Services;

public static class FileSystemApplicationSettingsPathProvider
{
    public static string GetDefaultSettingsFilePath()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "StructVault", "settings.xml");
    }
}
