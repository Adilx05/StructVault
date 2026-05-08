using System.Windows.Input;

namespace StructVault.Desktop.Commands;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<object?, CancellationToken, Task> executeAsync;
    private readonly Predicate<object?>? canExecute;
    private readonly bool allowsConcurrentExecutions;
    private int executionCount;

    public AsyncCommand(Func<Task> executeAsync, Func<bool>? canExecute = null, bool allowsConcurrentExecutions = false)
        : this(
            (_, _) => executeAsync(),
            canExecute is null ? null : _ => canExecute(),
            allowsConcurrentExecutions)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
    }

    public AsyncCommand(
        Func<CancellationToken, Task> executeAsync,
        Func<bool>? canExecute = null,
        bool allowsConcurrentExecutions = false)
        : this(
            (_, cancellationToken) => executeAsync(cancellationToken),
            canExecute is null ? null : _ => canExecute(),
            allowsConcurrentExecutions)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);
    }

    public AsyncCommand(
        Func<object?, CancellationToken, Task> executeAsync,
        Predicate<object?>? canExecute = null,
        bool allowsConcurrentExecutions = false)
    {
        this.executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        this.canExecute = canExecute;
        this.allowsConcurrentExecutions = allowsConcurrentExecutions;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool IsExecuting => executionCount > 0;

    public bool CanExecute(object? parameter)
    {
        if (!allowsConcurrentExecutions && IsExecuting)
        {
            return false;
        }

        return canExecute?.Invoke(parameter) ?? true;
    }

    public async void Execute(object? parameter)
    {
        await ExecuteAsync(parameter, CancellationToken.None).ConfigureAwait(true);
    }

    public async Task ExecuteAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        BeginExecution();

        try
        {
            await executeAsync(parameter, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            EndExecution();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }

    private void BeginExecution()
    {
        executionCount++;
        RaiseCanExecuteChanged();
    }

    private void EndExecution()
    {
        executionCount--;
        RaiseCanExecuteChanged();
    }
}
