using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

namespace StructVault.Desktop;

public partial class App : System.Windows.Application
{
    private TaskbarIcon? trayIcon;
    private string? startupVaultFilePath;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        startupVaultFilePath = GetRequestedVaultFilePath(e.Args);
        InitializeTrayIcon();
        MainWindow window = new(startupVaultFilePath);
        MainWindow = window;
        window.Show();
    }

    private void InitializeTrayIcon()
    {
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.ico");

        trayIcon = new TaskbarIcon
        {
            ToolTipText = "StructVault",
            Icon = new System.Drawing.Icon(iconPath)
        };

        trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;

        var contextMenu = new System.Windows.Controls.ContextMenu();

        var openGitHubItem = new System.Windows.Controls.MenuItem { Header = "GitHub" };
        openGitHubItem.Click += OpenGitHub_Click;
        contextMenu.Items.Add(openGitHubItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += Exit_Click;
        contextMenu.Items.Add(exitItem);

        trayIcon.ContextMenu = contextMenu;
    }

    private void TrayIcon_TrayMouseDoubleClick(object? sender, RoutedEventArgs e)
    {
        if (MainWindow != null)
        {
            if (MainWindow.WindowState == WindowState.Minimized)
            {
                MainWindow.WindowState = WindowState.Normal;
            }
            MainWindow.Show();
            MainWindow.Activate();
        }
    }

    private void OpenGitHub_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Adilx05/StructVault",
            UseShellExecute = true
        });
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        RequestShutdown();
    }

    public void RequestShutdown()
    {
        if (trayIcon != null)
        {
            trayIcon.Dispose();
            trayIcon = null;
        }
        Dispatcher.InvokeShutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (trayIcon != null)
        {
            trayIcon.Dispose();
            trayIcon = null;
        }
        base.OnExit(e);
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
