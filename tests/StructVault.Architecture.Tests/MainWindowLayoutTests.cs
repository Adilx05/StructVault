using System.Xml.Linq;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class MainWindowLayoutTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void MainWindowDefinesTreeAndDetailWorkspaceColumns()
    {
        XDocument mainWindow = XDocument.Load(GetRepositoryFile("src/StructVault.Desktop/MainWindow.xaml"));

        XElement workspaceGrid = GetWorkspaceGrid(mainWindow);
        List<XElement> columns = workspaceGrid
            .Element(PresentationNamespace + "Grid.ColumnDefinitions")?
            .Elements(PresentationNamespace + "ColumnDefinition")
            .ToList() ?? [];

        Assert.Equal(3, columns.Count);
        Assert.Equal("300", (string?)columns[0].Attribute("Width"));
        Assert.Equal("240", (string?)columns[0].Attribute("MinWidth"));
        Assert.Equal("8", (string?)columns[1].Attribute("Width"));
        Assert.Equal("*", (string?)columns[2].Attribute("Width"));
        Assert.Equal("420", (string?)columns[2].Attribute("MinWidth"));
    }

    [Fact]
    public void MainWindowContainsVaultTreeAndDetailPanel()
    {
        XDocument mainWindow = XDocument.Load(GetRepositoryFile("src/StructVault.Desktop/MainWindow.xaml"));

        XElement vaultTreeView = Assert.Single(mainWindow.Descendants(PresentationNamespace + "TreeView"));
        XElement detailScrollViewer = Assert.Single(mainWindow
            .Descendants(PresentationNamespace + "ScrollViewer")
            .Where(element => (string?)element.Attribute(XamlNamespace + "Name") == "VaultDetailScrollViewer"));

        Assert.Equal("VaultTreeView", (string?)vaultTreeView.Attribute(XamlNamespace + "Name"));
        Assert.Equal("1", (string?)vaultTreeView.Attribute("Grid.Row"));
        Assert.Equal("1", (string?)detailScrollViewer.Attribute("Grid.Row"));
        Assert.Contains(mainWindow.Descendants(PresentationNamespace + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Vault structure");
        Assert.Contains(mainWindow.Descendants(PresentationNamespace + "TextBlock"),
            element => (string?)element.Attribute("Text") == "Details");
    }

    [Fact]
    public void MainWindowBindsVaultTreeToNodeHierarchy()
    {
        XDocument mainWindow = XDocument.Load(GetRepositoryFile("src/StructVault.Desktop/MainWindow.xaml"));

        XElement vaultTreeView = Assert.Single(mainWindow.Descendants(PresentationNamespace + "TreeView"));
        XElement template = Assert.Single(mainWindow.Descendants(PresentationNamespace + "HierarchicalDataTemplate"));
        XElement templateText = Assert.Single(template.Descendants(PresentationNamespace + "TextBlock"));

        Assert.Equal("{Binding VaultNodes}", (string?)vaultTreeView.Attribute("ItemsSource"));
        Assert.Equal("{Binding Children}", (string?)template.Attribute("ItemsSource"));
        Assert.Equal("{x:Type viewModels:VaultTreeNodeViewModel}", (string?)template.Attribute("DataType"));
        Assert.Equal("{Binding Name}", (string?)templateText.Attribute("Text"));
    }

    [Fact]
    public void MainWindowProvidesResizableWorkspaceSplitter()
    {
        XDocument mainWindow = XDocument.Load(GetRepositoryFile("src/StructVault.Desktop/MainWindow.xaml"));

        XElement splitter = Assert.Single(mainWindow.Descendants(PresentationNamespace + "GridSplitter"));

        Assert.Equal("1", (string?)splitter.Attribute("Grid.Column"));
        Assert.Equal("PreviousAndNext", (string?)splitter.Attribute("ResizeBehavior"));
        Assert.Equal("Columns", (string?)splitter.Attribute("ResizeDirection"));
    }

    private static XElement GetWorkspaceGrid(XDocument mainWindow)
    {
        return mainWindow
            .Descendants(PresentationNamespace + "Grid")
            .Single(grid => (string?)grid.Attribute("Grid.Row") == "1" &&
                grid.Element(PresentationNamespace + "Grid.ColumnDefinitions") is not null);
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
