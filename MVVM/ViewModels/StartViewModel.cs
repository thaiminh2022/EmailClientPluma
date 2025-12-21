using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;


namespace EmailClientPluma.MVVM.ViewModels
{
    internal class StartViewModel : ObserableObject, IRequestClose
    {
        private readonly IAccountService _accountService;

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
            }
        }
        private Account? _selectedAccount;

        public StartViewModel(IAccountService accountService)
        {
            _accountService = accountService;

            // initialize observable collection from service
            Accounts = _accountService.GetAccounts();

            AddAccountGoogleCommand = new RelayCommandAsync(async _ =>
            {
                await _accountService.AddAccountAsync(Provider.Google);
            });

            AddAccountMicrosoftCommand = new RelayCommandAsync(async _ =>
            {
                await _accountService.AddAccountAsync(Provider.Microsoft);
            });
        }

        public event EventHandler<bool?>? RequestClose;
        public StartViewModel() { }
    }
}
