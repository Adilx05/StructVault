using System.IO;
using System.Windows;

namespace StructVault.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? requestedVaultFilePath = GetRequestedVaultFilePath(e.Args);
        MainWindow window = new(requestedVaultFilePath);
        MainWindow = window;
        window.Show();
    }

    private static string? GetRequestedVaultFilePath(IEnumerable<string> args)
    {
        foreach (string arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            string candidate = arg.Trim();
            if (candidate.EndsWith(".qps", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
