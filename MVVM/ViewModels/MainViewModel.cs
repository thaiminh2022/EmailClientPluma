using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using System.Collections.ObjectModel;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class MainViewModel : ObserableObject
    {
        readonly IAccountService _accountService;

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


        public RelayCommand AddAccount { get; set; }
        public MainViewModel(IAccountService accountService)
        {
            _accountService = accountService;
            Accounts = _accountService.GetAccounts();

            AddAccount = new RelayCommand(async _ =>
            {
                // TODO: ADd more provider
                await _accountService.AddAccountAsync(Provider.Google);
            });
        }

        public MainViewModel() { }
    }
}
