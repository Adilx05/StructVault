using StructVault.Desktop.Commands;
using StructVault.Desktop.ViewModels;
using Xunit;

namespace StructVault.Architecture.Tests;

public sealed class MvvmBaseTests
{
    [Fact]
    public void SetPropertyRaisesPropertyChangedWhenValueChanges()
    {
        TestViewModel viewModel = new();
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        bool changed = viewModel.UpdateName("Vault");

        Assert.True(changed);
        Assert.Equal("Vault", viewModel.Name);
        Assert.Equal([nameof(TestViewModel.Name)], changedProperties);
    }

    [Fact]
    public void SetPropertyDoesNotRaisePropertyChangedWhenValueIsUnchanged()
    {
        TestViewModel viewModel = new();
        Assert.True(viewModel.UpdateName("Vault"));

        bool changed = viewModel.UpdateName("Vault");

        Assert.False(changed);
    }

    [Fact]
    public void OnPropertyChangedRequiresPropertyName()
    {
        TestViewModel viewModel = new();

        Assert.Throws<ArgumentException>(() => viewModel.RaiseInvalidPropertyChanged());
    }

    [Fact]
    public void RelayCommandExecutesWhenAllowed()
    {
        int executions = 0;
        RelayCommand command = new(_ => executions++, _ => true);

        command.Execute(null);

        Assert.Equal(1, executions);
    }

    [Fact]
    public void RelayCommandDoesNotExecuteWhenDisallowed()
    {
        int executions = 0;
        RelayCommand command = new(_ => executions++, _ => false);

        command.Execute(null);

        Assert.Equal(0, executions);
    }

    [Fact]
    public void RelayCommandRequiresExecuteDelegate()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action<object?>)null!));
    }

    [Fact]
    public async Task AsyncCommandPreventsConcurrentExecutionByDefault()
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int executions = 0;
        AsyncCommand command = new(async () =>
        {
            executions++;
            await completion.Task;
        });

        Task firstExecution = command.ExecuteAsync();

        Assert.False(command.CanExecute(null));
        await command.ExecuteAsync();
        Assert.Equal(1, executions);

        completion.SetResult();
        await firstExecution;

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task AsyncCommandResetsExecutionStateAfterFailure()
    {
        AsyncCommand command = new(() => throw new InvalidOperationException("Command failed."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());

        Assert.False(command.IsExecuting);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void AsyncCommandRequiresExecuteDelegate()
    {
        Assert.Throws<ArgumentNullException>(() => new AsyncCommand((Func<object?, CancellationToken, Task>)null!));
    }

    private sealed class TestViewModel : ViewModelBase
    {
        private string name = string.Empty;

        public string Name => name;

        public bool UpdateName(string value)
        {
            return SetProperty(ref name, value, nameof(Name));
        }

        public void RaiseInvalidPropertyChanged()
        {
            OnPropertyChanged(string.Empty);
        }
    }
}
