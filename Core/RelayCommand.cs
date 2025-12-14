using System.Windows.Input;

namespace EmailClientPluma.Core;

/// <summary>
///     Relaying command from UI to view models,
///     Binds to data context
/// </summary>
internal class RelayCommand : ICommand
{
    protected readonly Predicate<object?>? _canExecute;
    protected readonly Action<object?> _execute;


    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute(parameter);
    }

    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}