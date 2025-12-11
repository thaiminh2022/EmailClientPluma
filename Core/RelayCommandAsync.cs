using System.Windows.Input;

namespace EmailClientPluma.Core
{
    internal class RelayCommandAsync : ICommand
    {
        protected readonly Func<object?, Task> _execute;
        protected readonly Predicate<object?>? _canExecute;
        private bool _isExecuting;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommandAsync(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            try
            {
                // 1. Set execution state and notify UI that CanExecute has changed (now false)
                _isExecuting = true;
                RaiseCanExecuteChanged();

                // 2. Execute the asynchronous task
                await _execute(parameter);
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Error("Error on RelayCommandAsync", ex);
            }
            finally
            {
                // 3. Reset execution state and notify UI that CanExecute has changed (now true)
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
