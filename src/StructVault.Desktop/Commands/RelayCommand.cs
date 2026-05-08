using System.Windows.Input;

namespace StructVault.Desktop.Commands;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> execute;
    private readonly Predicate<object?>? canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(
            _ => execute(),
            canExecute is null ? null : _ => canExecute())
    {
        ArgumentNullException.ThrowIfNull(execute);
    }

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke(parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        execute(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
