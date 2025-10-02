using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.MVVM.Views;
using System.Collections.ObjectModel;
using System.Windows;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class MainViewModel : ObserableObject
    {
        readonly IAccountService _accountService;
        readonly IWindowFactory _windowFactory;

        // A list of logined account
        public ObservableCollection<Account> Accounts { get; private set; }
        // Account selected in the list view

        private Account? _selectedAccount;
        public Account? SelectedAccount
        {
            get { return _selectedAccount; }
            set
            {
                _selectedAccount = value;
                var _ = ValidateSelected();
                OnPropertyChanges();
            }
        }

        async Task ValidateSelected()
        {
            bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);
            if (!isValid)
            {
                _selectedAccount = null;
                //MessageBox.Show("Not valid");
            }
            else
            {
                //MessageBox.Show("Valid");
            }

        }
        private Email? _selectedEmail;

        public Email? SelectedEmail
        {
            get { return _selectedEmail; }
            set
            {
                _selectedEmail = value;
                OnPropertyChanges();
            }
        }


        public RelayCommand AddAccountCommand { get; set; }
        public RelayCommand ComposeCommand { get; set; }

        public MainViewModel(IAccountService accountService, IWindowFactory windowFactory)
        {
            _accountService = accountService;
            _windowFactory = windowFactory;
            
            Accounts = _accountService.GetAccounts();

            AddAccountCommand = new RelayCommand(async _ =>
            {
                // TODO: ADd more provider
                await _accountService.AddAccountAsync(Provider.Google);
            });

            ComposeCommand = new RelayCommand(_ =>
            {
                var newEmailWindow = _windowFactory.CreateWindow<NewEmailView, NewEmailViewModel>();
                var confirmSend = newEmailWindow.ShowDialog();

                if (confirmSend is not null && confirmSend == true)
                {
                    // start sending them emails
                    MessageBox.Show("User wanna send emails");
                }
            });
        }
        public MainViewModel() { }
    }
}
