using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.MVVM.Views;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
            if (_selectedAccount is null || _selectedAccount.IsHeadersFetched)
            {
                Mouse.OverrideCursor = null;
                return;
            }


            bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);


            if (!isValid)
            {
                Mouse.OverrideCursor = null;
            }

            await _emailService.FetchEmailHeaderAsync(_selectedAccount);
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

            Accounts = _accountService.GetAccounts();
            Accounts.CollectionChanged += AccountsCollectionChanged;
            foreach (var account in Accounts)
            {
                _emailService.StartRealtimeUpdates(account);
            }

            _emailService.EmailReceived += OnEmailReceived;

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
                        var accountToRemove = SelectedAccount;
                        if (accountToRemove is null) break;
                        _emailService.StopRealtimeUpdates(accountToRemove);
                        _accountService.RemoveAccountAsync(accountToRemove);
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

        void AccountsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (Account account in e.NewItems)
                {
                    _emailService.StartRealtimeUpdates(account);
                }
            }

            if (e.OldItems is not null)
            {
                foreach (Account account in e.OldItems)
                {
                    _emailService.StopRealtimeUpdates(account);
                }
            }
        }

        void OnEmailReceived(object? sender, EmailReceivedEventArgs e)
        {
            void AddEmail()
            {
                var account = Accounts.FirstOrDefault(acc => acc.ProviderUID == e.Account.ProviderUID);
                if (account is null)
                {
                    return;
                }

                if (account.Emails.Any(mail => mail.MessageIdentifiers.ImapUID == e.Email.MessageIdentifiers.ImapUID))
                {
                    return;
                }

                account.Emails.Insert(0, e.Email);
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(AddEmail);
            }
            else
            {
                AddEmail();
            }
        }
    }
}
