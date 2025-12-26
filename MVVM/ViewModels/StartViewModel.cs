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
                await accountService.AddAccountAsync(Provider.Google);
            });

            AddAccountMicrosoftCommand = new RelayCommandAsync(async _ =>
            {
                await accountService.AddAccountAsync(Provider.Microsoft);
            });
        }

        public event EventHandler<bool?>? RequestClose;
        public StartViewModel() { }
    }
}
