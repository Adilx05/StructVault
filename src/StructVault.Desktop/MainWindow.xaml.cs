using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private async void VaultTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.SelectVaultNodeAsync(e.NewValue as VaultTreeNodeViewModel).ConfigureAwait(true);
        }
    }
}
