using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using StructVault.Application.Persistence;
using StructVault.Desktop.Composition;
using StructVault.Desktop.ViewModels;

namespace StructVault.Desktop;

public partial class MainWindow : MetroWindow
{
    private bool closeConfirmed;
    private bool closeConfirmationInProgress;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureDefaultViewModel();
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Closing += MainWindowClosing;
    }

    private void ConfigureDefaultViewModel()
    {
        DesktopVaultSender sender = new();
        DataContext = new MainWindowViewModel(sender);
        Closing += MainWindowClosing;
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

    private async void MainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (closeConfirmed || DataContext is not MainWindowViewModel viewModel || !viewModel.IsDirty)
        {
            return;
        }

        e.Cancel = true;
        if (closeConfirmationInProgress)
        {
            return;
        }

        closeConfirmationInProgress = true;
        try
        {
            bool shouldClose = await viewModel.ConfirmExitAsync().ConfigureAwait(true);
            if (shouldClose)
            {
                closeConfirmed = true;
                Close();
            }
        }
        finally
        {
            closeConfirmationInProgress = false;
        }
    }

    private async void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is ListBox { SelectedItem: VaultSearchResultViewModel result })
        {
            await viewModel.SelectSearchResultAsync(result).ConfigureAwait(true);
        }
    }

    private async void VaultTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.SelectVaultNodeAsync(e.NewValue as VaultTreeNodeViewModel).ConfigureAwait(true);
        }
    }
}
