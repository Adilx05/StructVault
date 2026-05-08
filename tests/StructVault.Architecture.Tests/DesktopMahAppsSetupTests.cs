using System.Xml.Linq;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class DesktopMahAppsSetupTests
{
    private const string MahAppsNamespace = "http://metro.mahapps.com/winfx/xaml/controls";
    private const string MahAppsPackageVersion = "2.4.11";

    [Fact]
    public void DesktopProjectReferencesMahAppsMetro()
    {
        XDocument project = XDocument.Load(GetRepositoryFile("src/StructVault.Desktop/StructVault.Desktop.csproj"));

        XElement? packageReference = project
            .Descendants("PackageReference")
            .SingleOrDefault(reference => (string?)reference.Attribute("Include") == "MahApps.Metro");

        Assert.NotNull(packageReference);
        Assert.Equal(MahAppsPackageVersion, (string?)packageReference.Attribute("Version"));
    }

    [Fact]
    public void AppMergesRequiredMahAppsResourceDictionaries()
    {
        string appXaml = File.ReadAllText(GetRepositoryFile("src/StructVault.Desktop/App.xaml"));

        Assert.Contains("MahApps.Metro;component/Styles/Controls.xaml", appXaml, StringComparison.Ordinal);
        Assert.Contains("MahApps.Metro;component/Styles/Fonts.xaml", appXaml, StringComparison.Ordinal);
        Assert.Contains("MahApps.Metro;component/Styles/Themes/Light.Blue.xaml", appXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowUsesMetroWindowShell()
    {
        XDocument mainWindow = XDocument.Load(GetRepositoryFile("src/StructVault.Desktop/MainWindow.xaml"));
        string codeBehind = File.ReadAllText(GetRepositoryFile("src/StructVault.Desktop/MainWindow.xaml.cs"));

        Assert.Equal(MahAppsNamespace, mainWindow.Root?.Name.NamespaceName);
        Assert.Equal("MetroWindow", mainWindow.Root?.Name.LocalName);
        Assert.Contains("MainWindow : MetroWindow", codeBehind, StringComparison.Ordinal);
    }

    private static string GetRepositoryFile(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TASKS.md")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("Could not locate the repository root containing TASKS.md.");
        }

        return Path.Combine(directory.FullName, relativePath);
    }
}
