using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.MVVM.Views;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class MainViewModel : ObserableObject
    {
        readonly IAccountService _accountService;
        readonly IEmailService _emailService;
        readonly IWindowFactory _windowFactory;
        readonly IEmailFilterService _filterService;

        // A list of logined account
        public ObservableCollection<Account> Accounts
        {
            get;
            private set;
        }
        // Account selected in the list view


        private Account? _selectedAccount;
        public Account? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                if (_selectedAccount == value) return;

                if (_selectedAccount != null)
                    _selectedAccount.Emails.CollectionChanged -= Emails_CollectionChanged;

                _selectedAccount = value;
                OnPropertyChanges();

                FilteredEmails.Clear();

                if (_selectedAccount != null)
                {
                    _selectedAccount.Emails.CollectionChanged += Emails_CollectionChanged;

                    // Initial fill
                    _ = UpdateFilteredEmailsAsync();
                    _ = FetchNewHeadersAndPrefetchBody();
                }

            }
        }

        private async void Emails_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            await UpdateFilteredEmailsAsync();
        }

        private async Task FetchNewHeadersAndPrefetchBody()
        {
            if (_selectedAccount is null)
                return;

            bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);

            if (!isValid || _selectedAccount.FirstTimeHeaderFetched)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            await _emailService.FetchEmailHeaderAsync(_selectedAccount);
            _selectedAccount.FirstTimeHeaderFetched = true;
            Mouse.OverrideCursor = null;

            await _emailService.PrefetchRecentBodiesAsync(_selectedAccount, 30);
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


                if (_selectedEmail is not null && _selectedEmail.BodyFetched)
                {
                    var result = PhishDetector.ValidateHtmlContent(_selectedEmail.MessageParts.Body ?? "");
                    MessageBoxHelper.Info(result.ToString());
                }

            }
        }

        private CancellationTokenSource? _filterCts; //cancellation when filtering
        public ObservableCollection<Email> FilteredEmails { get; private set; }
        public EmailFilterOptions Filters { get; } = new(); // Filter options

        async Task FetchEmailBody()
        {
            if (_selectedAccount is null || _selectedEmail is null || _selectedEmail.BodyFetched)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);

            if (!isValid)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            await _emailService.FetchEmailBodyAsync(_selectedAccount, _selectedEmail);

            Mouse.OverrideCursor = null;
        }


        public RelayCommand AddAccountCommand { get; set; }
        public RelayCommand ComposeCommand { get; set; }
        public RelayCommand ReplyCommand { get; set; }
        public RelayCommand RemoveAccountCommand { get; set; }

        public MainViewModel(IAccountService accountService, IWindowFactory windowFactory, IEmailService emailService, IEmailFilterService emailFilterService)
        {
            _accountService = accountService;
            _windowFactory = windowFactory;
            _emailService = emailService;
            _filterService = emailFilterService;

            // make list auto sort descending by date
            FilteredEmails = [];
            Filters.PropertyChanged += async (s, e) => await UpdateFilteredEmailsAsync();


            Accounts = _accountService.GetAccounts();
            SelectedAccount = Accounts.First();

            // COMMANDS
            AddAccountCommand = new RelayCommand(async _ =>
            {
                // TODO: ADd more provider
                await _accountService.AddAccountAsync(Provider.Google);
            });

            ComposeCommand = new RelayCommand(_ =>
            {
                var newEmailWindow = _windowFactory.CreateWindow<NewEmailView, NewEmailViewModel>();
                bool? sucess = newEmailWindow.ShowDialog();

                if (sucess is null)
                    return;

                if (sucess == true)
                {
                    MessageBoxHelper.Info("Message was sent");
                }
            }, _ => Accounts.Count > 0);

            RemoveAccountCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null) return;

                var result = MessageBoxHelper.Confirmation($"Are you sure to remove {SelectedAccount.Email}?");
                if (result is null || result is false) return;

                _accountService.RemoveAccountAsync(SelectedAccount);
                SelectedAccount = null;
            }, _ => SelectedAccount is not null);

            ReplyCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null || SelectedEmail == null) return;
                // too lazy to implement for now

            }, _ => SelectedAccount is not null && SelectedEmail is not null &&
                    SelectedEmail.MessageParts.From != SelectedAccount.Email);
        }

        #region Filtered Emails
        public async Task UpdateFilteredEmailsAsync()
        {
            if (SelectedAccount == null)
            {
                FilteredEmails.Clear();
                return;
            }

            // Cancel previous filter if running
            _filterCts?.Cancel();
            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;

            FilteredEmails.Clear();
            foreach (var email in SelectedAccount.Emails.OrderByDescending(x => x.MessageParts.Date))
            {
                if (await _filterService.MatchFiltersAsync(email, Filters, token))
                {
                    FilteredEmails.Add(email);
                }
            }
        }
        #endregion

        // This should not be matter because this is for UI type hinting
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public MainViewModel()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
        }
    }
}