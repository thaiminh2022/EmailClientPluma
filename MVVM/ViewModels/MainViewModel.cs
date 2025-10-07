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
        readonly IEmailService _emailService;
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
                var _ = FetchEmailHeaders();
                OnPropertyChanges();
            }
        }

        async Task FetchEmailHeaders()
        {
            if (_selectedAccount is null) return;

            bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);
            if (!isValid) return;

            await _emailService.FetchEmailHeaderAsync(_selectedAccount);
        }
        private Email? _selectedEmail;

        public Email? SelectedEmail
        {
            get { return _selectedEmail; }
            set
            {
                _selectedEmail = value;
                var _ = FetchEmailBody();
                OnPropertyChanges();
            }
        }

        async Task FetchEmailBody()
        {
            if (_selectedAccount is null || _selectedEmail is null) return;
            await _emailService.FetchEmailBodyAsync(_selectedAccount, _selectedEmail);
        }


        public RelayCommand AddAccountCommand { get; set; }
        public RelayCommand ComposeCommand { get; set; }
        public RelayCommand ReplyCommand { get; set; }
        public RelayCommand RemoveAccountCommand { get; set; }

        public MainViewModel(IAccountService accountService, IWindowFactory windowFactory, IEmailService emailService)
        {
            _accountService = accountService;
            _windowFactory = windowFactory;
            _emailService = emailService;

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
            }, _ => Accounts.Count > 0);

            RemoveAccountCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null) return;

                var result = MessageBox.Show($"Are you sure to remove {SelectedAccount.Email}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        _accountService.RemoveAccountAsync(SelectedAccount);
                        SelectedAccount = null;
                        break;
                    default:
                        return;
                };

            }, _ => SelectedAccount is not null);

            ReplyCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null || SelectedEmail == null) return;
                // too lazy to implement for now

            }, _ => SelectedAccount is not null && SelectedEmail is not null &&
                    SelectedEmail.MessageParts.From != SelectedAccount.Email);
        }
        public MainViewModel()
        {
        }
    }
}
