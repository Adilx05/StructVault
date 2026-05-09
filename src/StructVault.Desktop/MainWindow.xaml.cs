using System.Windows;
using MahApps.Metro.Controls;
using StructVault.Desktop.ViewModels;

namespace StructVault.Desktop;

public partial class MainWindow : MetroWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private async void VaultTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.SelectVaultNodeAsync(e.NewValue as VaultTreeNodeViewModel).ConfigureAwait(true);
        }
    }
}
