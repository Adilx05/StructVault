using System.Xml.Linq;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class InstallerConfigurationTests
{
    [Fact]
    public void InstallerRegistersQpsFilesToOpenWithStructVault()
    {
        string package = File.ReadAllText(GetRepositoryFile("installer/StructVault.Installer/Package.wxs"));

        Assert.Contains("Key=\".qps\"", package, StringComparison.Ordinal);
        Assert.Contains("Value=\"StructVault.qps\"", package, StringComparison.Ordinal);
        Assert.Contains("StructVault.qps\\shell\\open\\command", package, StringComparison.Ordinal);
        Assert.Contains("StructVault.Desktop.exe&quot; &quot;%1", package, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerProvidesStandardWizardLicenseInstallDirectoryAndShortcuts()
    {
        string package = File.ReadAllText(GetRepositoryFile("installer/StructVault.Installer/Package.wxs"));
        string installerProject = File.ReadAllText(GetRepositoryFile("installer/StructVault.Installer/StructVault.Installer.wixproj"));

        Assert.Contains("<PackageReference Include=\"WixToolset.UI.wixext\" Version=\"5.0.2\" />", installerProject, StringComparison.Ordinal);
        Assert.Contains("<ui:WixUI Id=\"WixUI_InstallDir\" InstallDirectory=\"INSTALLFOLDER\" />", package, StringComparison.Ordinal);
        Assert.Contains("<WixVariable Id=\"WixUILicenseRtf\" Value=\"License.rtf\" />", package, StringComparison.Ordinal);
        Assert.Contains("<StandardDirectory Id=\"ProgramMenuFolder\">", package, StringComparison.Ordinal);
        Assert.Contains("<StandardDirectory Id=\"DesktopFolder\" />", package, StringComparison.Ordinal);
        Assert.Contains("<Component Id=\"DesktopShortcut\" Directory=\"DesktopFolder\"", package, StringComparison.Ordinal);
        Assert.Contains("Directory=\"DesktopFolder\"", package, StringComparison.Ordinal);
        Assert.Contains("Root=\"HKCU\" Key=\"Software\\StructVault\" Name=\"DesktopShortcut\"", package, StringComparison.Ordinal);
        Assert.Contains("Root=\"HKCU\" Key=\"Software\\StructVault\" Name=\"StartMenuShortcuts\"", package, StringComparison.Ordinal);
        Assert.Contains("Id=\"StructVaultStartMenuShortcut\"", package, StringComparison.Ordinal);
        Assert.Contains("Id=\"StructVaultUninstallShortcut\"", package, StringComparison.Ordinal);
        Assert.Contains("Target=\"[System64Folder]msiexec.exe\"", package, StringComparison.Ordinal);
        Assert.Contains("Arguments=\"/x [ProductCode]\"", package, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerUsesValidSemanticVersion()
    {
        XDocument versions = XDocument.Load(GetRepositoryFile("Directory.Build.props"));
        string? major = versions.Descendants("VersionMajor").Single().Value;
        string? minor = versions.Descendants("VersionMinor").Single().Value;
        string? patch = versions.Descendants("VersionPatch").Single().Value;
        string installerProject = File.ReadAllText(GetRepositoryFile("installer/StructVault.Installer/StructVault.Installer.wixproj"));

        Assert.True(int.TryParse(major, out int majorNum) && majorNum >= 1, "Major version must be >= 1");
        Assert.True(int.TryParse(minor, out int minorNum) && minorNum >= 0, "Minor version must be >= 0");
        Assert.True(int.TryParse(patch, out int patchNum) && patchNum >= 0, "Patch version must be >= 0");
        string desktopProject = File.ReadAllText(GetRepositoryFile("src/StructVault.Desktop/StructVault.Desktop.csproj"));

        Assert.Contains("<ProductVersion>$(Version)</ProductVersion>", installerProject, StringComparison.Ordinal);
        Assert.Contains("<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>", desktopProject, StringComparison.Ordinal);
        Assert.Contains("Targets=\"Restore;Publish\"", installerProject, StringComparison.Ordinal);
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
