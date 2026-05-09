using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using StructVault.Application.Abstractions.Logging;
using StructVault.Application.Logging;
using StructVault.Application.Persistence;
using StructVault.Desktop.Composition;
using StructVault.Desktop.ViewModels;

namespace StructVault.Desktop;

public partial class MainWindow : MetroWindow
{
    private static readonly TimeSpan ActivityReportThrottle = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan IdleLockCheckInterval = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer idleLockTimer;
    private bool closeConfirmed;
    private bool closeConfirmationInProgress;
    private Point nodeDragStartPoint;
    private Point fieldDragStartPoint;
    private VaultTreeNodeViewModel? draggedNode;
    private VaultFieldViewModel? draggedField;
    private DateTimeOffset lastActivityReportUtc = DateTimeOffset.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        idleLockTimer = CreateIdleLockTimer();
        ConfigureDefaultViewModel();
        ConfigureActivityTracking();
        ConfigureIdleLockMonitoring();
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        idleLockTimer = CreateIdleLockTimer();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Closing += MainWindowClosing;
        ConfigureActivityTracking();
        ConfigureIdleLockMonitoring();
    }

    private static DispatcherTimer CreateIdleLockTimer()
    {
        return new DispatcherTimer
        {
            Interval = IdleLockCheckInterval
        };
    }

    private void ConfigureActivityTracking()
    {
        Loaded += MainWindowLoaded;
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(UserActivityDetected), true);
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(UserActivityDetected), true);
        AddHandler(Mouse.PreviewMouseWheelEvent, new MouseWheelEventHandler(UserActivityDetected), true);
        AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(UserActivityDetected), true);
        AddHandler(Stylus.PreviewStylusDownEvent, new StylusDownEventHandler(UserActivityDetected), true);
    }

    private void ConfigureIdleLockMonitoring()
    {
        idleLockTimer.Tick += IdleLockTimerTick;
        Loaded += (_, _) => idleLockTimer.Start();
        Closed += (_, _) => idleLockTimer.Stop();
    }

    private void ConfigureDefaultViewModel()
    {
        DesktopVaultSender sender = new();
        DataContext = new MainWindowViewModel(sender);
        Closing += MainWindowClosing;
        Loaded += async (_, _) => await LoadInitialVaultAsync(sender).ConfigureAwait(true);
    }

    private async void MainWindowLoaded(object sender, RoutedEventArgs e)
    {
        await ReportUserActivityAsync(force: true).ConfigureAwait(true);
    }

    private async void UserActivityDetected(object sender, RoutedEventArgs e)
    {
        await ReportUserActivityAsync(force: false).ConfigureAwait(true);
    }

    private async void IdleLockTimerTick(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.LockVaultAfterIdleTimeoutAsync().ConfigureAwait(true);
    }

    private async Task ReportUserActivityAsync(bool force)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!force && now - lastActivityReportUtc < ActivityReportThrottle)
        {
            return;
        }

        lastActivityReportUtc = now;
        await viewModel.RecordUserActivityAsync().ConfigureAwait(true);
    }

    private async Task LoadInitialVaultAsync(DesktopVaultSender sender)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            System.Data.Common.DbConnection connection = await sender
                .Send(new CreateInMemoryVaultDatabaseCommand(), CancellationToken.None)
                .ConfigureAwait(true);
            await viewModel.LoadVaultTreeAsync(connection).ConfigureAwait(true);
            await WriteOperationalLogAsync(sender, ApplicationLogLevel.Information, "Desktop", "InitialVaultLoaded", null)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await WriteOperationalLogAsync(sender, ApplicationLogLevel.Error, "Desktop", "InitialVaultLoadFailed", ex.GetType().FullName)
                .ConfigureAwait(true);
            throw;
        }
    }

    private static async Task WriteOperationalLogAsync(
        DesktopVaultSender sender,
        ApplicationLogLevel level,
        string category,
        string eventName,
        string? detail)
    {
        try
        {
            await sender.Send(new WriteApplicationLogCommand(level, category, eventName, detail), CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
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

    private void VaultTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        nodeDragStartPoint = e.GetPosition(VaultTreeView);
        draggedNode = FindTreeViewNode(e.OriginalSource as DependencyObject);
    }

    private void VaultTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedNode is null)
        {
            return;
        }

        Point currentPosition = e.GetPosition(VaultTreeView);
        if (Math.Abs(currentPosition.X - nodeDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - nodeDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(VaultTreeView, draggedNode, DragDropEffects.Move);
        draggedNode = null;
    }

    private async void VaultTreeView_Drop(object sender, DragEventArgs e)
    {
        VaultTreeNodeViewModel? sourceNode = e.Data.GetData(typeof(VaultTreeNodeViewModel)) as VaultTreeNodeViewModel;
        VaultTreeNodeViewModel? targetNode = FindTreeViewNode(e.OriginalSource as DependencyObject);
        draggedNode = null;

        if (DataContext is not MainWindowViewModel viewModel || sourceNode is null || targetNode is null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        bool reordered = await viewModel.ReorderVaultNodeAsync(sourceNode, targetNode).ConfigureAwait(true);
        e.Effects = reordered ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void VaultTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.SelectVaultNodeAsync(e.NewValue as VaultTreeNodeViewModel).ConfigureAwait(true);
        }
    }

    private void VaultFieldsItemsControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        fieldDragStartPoint = e.GetPosition(VaultFieldsItemsControl);
        draggedField = FindFieldViewModel(e.OriginalSource as DependencyObject);
    }

    private void VaultFieldsItemsControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedField is null)
        {
            return;
        }

        Point currentPosition = e.GetPosition(VaultFieldsItemsControl);
        if (Math.Abs(currentPosition.X - fieldDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPosition.Y - fieldDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(VaultFieldsItemsControl, draggedField, DragDropEffects.Move);
        draggedField = null;
    }

    private async void VaultFieldsItemsControl_Drop(object sender, DragEventArgs e)
    {
        VaultFieldViewModel? sourceField = e.Data.GetData(typeof(VaultFieldViewModel)) as VaultFieldViewModel;
        VaultFieldViewModel? targetField = FindFieldViewModel(e.OriginalSource as DependencyObject);
        draggedField = null;

        if (DataContext is not MainWindowViewModel viewModel || sourceField is null || targetField is null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        bool reordered = await viewModel.ReorderVaultFieldAsync(sourceField, targetField).ConfigureAwait(true);
        e.Effects = reordered ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private static VaultTreeNodeViewModel? FindTreeViewNode(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is TreeViewItem { DataContext: VaultTreeNodeViewModel node })
            {
                return node;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static VaultFieldViewModel? FindFieldViewModel(DependencyObject? source)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: VaultFieldViewModel field })
            {
                return field;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
