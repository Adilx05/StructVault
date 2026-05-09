using System.Windows;
using MahApps.Metro.Controls;
using StructVault.Application.Persistence;
using StructVault.Desktop.Composition;
using StructVault.Desktop.ViewModels;

namespace StructVault.Desktop;

public partial class MainWindow : MetroWindow
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureDefaultViewModel();
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private void ConfigureDefaultViewModel()
    {
        DesktopVaultSender sender = new();
        DataContext = new MainWindowViewModel(sender);
        Loaded += async (_, _) => await LoadInitialVaultAsync(sender).ConfigureAwait(true);
    }

    private async Task LoadInitialVaultAsync(DesktopVaultSender sender)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        System.Data.Common.DbConnection connection = await sender
            .Send(new CreateInMemoryVaultDatabaseCommand(), CancellationToken.None)
            .ConfigureAwait(true);
        await viewModel.LoadVaultTreeAsync(connection).ConfigureAwait(true);
    }

    private async void VaultTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.SelectVaultNodeAsync(e.NewValue as VaultTreeNodeViewModel).ConfigureAwait(true);
        }
    }
}
