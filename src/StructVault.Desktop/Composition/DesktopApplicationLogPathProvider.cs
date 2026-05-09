using System.IO;

namespace StructVault.Desktop.Composition;

internal static class DesktopApplicationLogPathProvider
{
    public static string GetDefaultLogFilePath()
    {
        string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.GetTempPath();
        }

        return Path.Combine(baseDirectory, "StructVault", "logs", "structvault.log");
    }
}
