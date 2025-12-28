using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.MVVM.Views;
using System.Collections.ObjectModel;
using System.Windows;


namespace EmailClientPluma.MVVM.ViewModels
{
    internal class StartViewModel : ObserableObject, IRequestClose
    {
        private readonly IWindowFactory _factory;
        public ObservableCollection<Account> Accounts { get; }

        public RelayCommandAsync AddAccountGoogleCommand { get; }
        public RelayCommandAsync AddAccountMicrosoftCommand { get; }
        public RelayCommand SkipAddAccountCommand { get; }

        public Account? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                _selectedAccount = value;
                OnPropertyChanges();


                if (value is null) return;

                var mainView = _factory.CreateWindow<MainView, MainViewModel>();
                mainView.Show();

                if (mainView.DataContext is MainViewModel mvm)
                {
                    mvm.SelectedAccount = value;
                }

                Application.Current.MainWindow = mainView;
                RequestClose?.Invoke(this, true);

            }
        }
        private Account? _selectedAccount;

        public StartViewModel(IAccountService accountService, IWindowFactory factory)
        {
            _factory = factory;

            // initialize observable collection from service
            Accounts = accountService.GetAccounts();

            AddAccountGoogleCommand = new RelayCommandAsync(async _ =>
            {
                try
                {

                    await accountService.AddAccountAsync(Provider.Google);
                }
                catch (Exception ex)
                {
                    MessageBoxHelper.Error(ex.Message);
                }
            });

            AddAccountMicrosoftCommand = new RelayCommandAsync(async _ =>
            {
                try
                {
                    await accountService.AddAccountAsync(Provider.Microsoft);
                }
                catch (Exception ex)
                {
                    MessageBoxHelper.Error(ex.Message);
                }
            });
            SkipAddAccountCommand = new RelayCommand(_ =>
            {
                var mainView = _factory.CreateWindow<MainView, MainViewModel>();
                mainView.Show();

                Application.Current.MainWindow = mainView;
                RequestClose?.Invoke(this, true);
            });
        }

        public event EventHandler<bool?>? RequestClose;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public StartViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    }
}
