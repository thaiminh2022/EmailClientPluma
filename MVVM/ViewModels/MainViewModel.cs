using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.MVVM.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

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
            Mouse.OverrideCursor = Cursors.Wait;
            if (_selectedAccount is null)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            if (_selectedAccount.IsHeadersFetched)
            {
                _emailService.StartRealtimeUpdates(_selectedAccount);
                Mouse.OverrideCursor = null;
                return;
            }


            bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);


            if (!isValid)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            await _emailService.FetchEmailHeaderAsync(_selectedAccount);
            _emailService.StartRealtimeUpdates(_selectedAccount);
            Mouse.OverrideCursor = null;
        }
        private Email? _selectedEmail;

        public Email? SelectedEmail
        {
            get { return _selectedEmail; }
            set
            {
                _selectedEmail = value;


                Mouse.OverrideCursor = Cursors.Wait;
                var _ = FetchEmailBody();
                OnPropertyChanges();
            }
        }

        async Task FetchEmailBody()
        {
            if (_selectedAccount is null || _selectedEmail is null || _selectedEmail.BodyFetched)
            {
                Mouse.OverrideCursor = null;
                return;
            };
            await _emailService.FetchEmailBodyAsync(_selectedAccount, _selectedEmail);
            Mouse.OverrideCursor = null;

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

            _emailService.EmailReceived += OnEmailReceived;

            Accounts = _accountService.GetAccounts();

            AddAccountCommand = new RelayCommand(async _ =>
            {
                // TODO: ADd more provider
                await _accountService.AddAccountAsync(Provider.Google);
            });

            ComposeCommand = new RelayCommand(_ =>
            {
                var newEmailWindow = _windowFactory.CreateWindow<NewEmailView, NewEmailViewModel>();
                newEmailWindow.Show();
            }, _ => Accounts.Count > 0);

            RemoveAccountCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null) return;

                var result = MessageBox.Show($"Are you sure to remove {SelectedAccount.Email}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        _emailService.StopRealtimeUpdates(SelectedAccount);
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

        void OnEmailReceived(object? sender, EmailReceivedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var account = Accounts.FirstOrDefault(a => a.ProviderUID == e.Account.ProviderUID);
                if (account is null)
                {
                    return;
                }

                account.Emails.Add(e.Email);
            });
        }
    }
}
