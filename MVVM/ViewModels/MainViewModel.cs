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
        #region Services
        readonly IAccountService _accountService;
        readonly IEmailService _emailService;
        readonly IWindowFactory _windowFactory;
        readonly IEmailFilterService _filterService;
        #endregion

        #region Accounts
        // A list of logined account
        public ObservableCollection<Account> Accounts { get; private set; }
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

                _currentPage = 0;
                CommandManager.InvalidateRequerySuggested();

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
        #endregion

        #region Emails
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
            if (_selectedAccount is null || _selectedEmail is null)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            void CheckPhishing()
            {
                var check = PhishDetector.ValidateHtmlContent(_selectedEmail.MessageParts.Body ?? "");
                if (check == PhishDetector.SuspiciousLevel.None || check == PhishDetector.SuspiciousLevel.Minor)
                {
                    return;
                }
                MessageBoxHelper.Info("Cảnh báo phishing: ", check.ToString());
            }

            if (_selectedEmail.BodyFetched)
            {
                Mouse.OverrideCursor = null;
                CheckPhishing();
                return;
            }

            bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);

            if (!isValid)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            await _emailService.FetchEmailBodyAsync(_selectedAccount, _selectedEmail);

            CheckPhishing();
            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Filter
        private CancellationTokenSource? _filterCts; //cancellation when filtering
        public ObservableCollection<Email> FilteredEmails { get; private set; }
        public EmailFilterOptions Filters { get; } = new(); // Filter options
        #endregion


        #region Paging
        private int _pageSize = 50;       // emails per page
        private int _currentPage = 0;     // 0-based
        private int _totalPages = 0;

        public int CurrentPage
        {
            get => _currentPage + 1;      // expose as 1-based for UI
        }

        public int TotalPages
        {
            get => _totalPages;
        }

        #endregion


        #region Commands
        public RelayCommand AddAccountCommand { get; set; }
        public RelayCommand ComposeCommand { get; set; }
        public RelayCommand ReplyCommand { get; set; }
        public RelayCommand RemoveAccountCommand { get; set; }
        public RelayCommand NextCommand { get; set; }
        public RelayCommand PreviousCommand { get; set; }
        #endregion

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
            //SelectedAccount = Accounts.First();

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
            ReplyCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null || SelectedEmail == null) return;
                var newEmailWindow = _windowFactory.CreateWindow<NewEmailView, NewEmailViewModel>();
                
                if (newEmailWindow.DataContext is not NewEmailViewModel vm) return;
                vm.SetupReply(SelectedAccount, SelectedEmail);

                bool? sucess = newEmailWindow.ShowDialog();
                if (sucess is null)
                    return;

                if (sucess == true)
                {
                    MessageBoxHelper.Info("Message was sent");
                }

            }, _ => SelectedAccount is not null && SelectedEmail is not null &&
                    SelectedEmail.MessageParts.From != SelectedAccount.Email);

            RemoveAccountCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null) return;

                var result = MessageBoxHelper.Confirmation($"Are you sure to remove {SelectedAccount.Email}?");
                if (result is null || result is false) return;

                _accountService.RemoveAccountAsync(SelectedAccount);
                SelectedAccount = null;
            }, _ => SelectedAccount is not null);


            NextCommand = new RelayCommand(async _ =>
            {
                if (SelectedAccount is null || SelectedAccount.NoMoreOlderEmail)
                    return;
                bool atLastPage = (_currentPage + 1 >= _totalPages);

                if (atLastPage)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    try
                    {
                        var gotMore = await _emailService.FetchOlderHeadersAsync(SelectedAccount, _pageSize);

                        // Rebuild pages after loading older headers
                        await UpdateFilteredEmailsAsync();
                        // If we still have no next page (no more server mails), just stop
                        if (!gotMore || _currentPage + 1 >= _totalPages)
                            return;
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }
                }
                _currentPage++;
                await UpdateFilteredEmailsAsync();
            }, _ => SelectedAccount is not null && !SelectedAccount.NoMoreOlderEmail);

            PreviousCommand = new RelayCommand(async _ =>
            {
                if (_currentPage <= 0) return;
                _currentPage--;
                await UpdateFilteredEmailsAsync();
            }, _ => SelectedAccount is not null && _currentPage > 0);
        }

        public async Task UpdateFilteredEmailsAsync()
        {
            if (SelectedAccount == null)
            {
                FilteredEmails.Clear();

                _totalPages = 0;
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanges(nameof(CurrentPage));
                OnPropertyChanges(nameof(TotalPages));

                return;
            }

            // Cancel previous filter if running
            _filterCts?.Cancel();
            _filterCts?.Dispose();
            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;
            try
            {

                var allFiltered = new List<Email>();
                foreach (var email in SelectedAccount.Emails)
                {
                    if (await _filterService.MatchFiltersAsync(email, Filters, token))
                    {
                        allFiltered.Add(email);
                    }
                }

                // 2. Sort (newest first) if you want
                allFiltered = [.. allFiltered.OrderByDescending(e => e.MessageParts.Date)];

                // 3. Compute total pages
                if (allFiltered.Count == 0)
                {
                    _totalPages = 0;
                }
                else
                {
                    _totalPages = (int)Math.Ceiling(allFiltered.Count / (double)_pageSize);
                }

                if (_currentPage >= _totalPages)
                    _currentPage = Math.Max(0, _totalPages - 1);

                // 4. Slice current page
                var pageItems = allFiltered
                    .Skip(_currentPage * _pageSize)
                    .Take(_pageSize);

                FilteredEmails.Clear();
                foreach (var email in pageItems)
                {
                    FilteredEmails.Add(email);
                }

                OnPropertyChanges(nameof(CurrentPage));
                OnPropertyChanges(nameof(TotalPages));

                CommandManager.InvalidateRequerySuggested();
            }
            catch (OperationCanceledException)
            {
                // normal exit
            }
        }

        // This should not be matter because this is for UI type hinting
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public MainViewModel()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
        }
    }
}