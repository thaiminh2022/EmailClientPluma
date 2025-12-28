using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.MVVM.Views;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using EmailClientPluma.Security.Services;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class MainViewModel : ObserableObject
    {
        #region Services
        readonly IAccountService _accountService;
        readonly IEmailService _emailService;
        readonly IWindowFactory _windowFactory;
        readonly IEmailFilterService _filterService;
        readonly IPhishingDetectionService _phishingService;
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
            if (_selectedAccount is null) return;

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

        private bool _isPhishing;
        public bool IsPhishing
        {
            get => _isPhishing;
            set { _isPhishing = value; OnPropertyChanges(); }
        }

        private double _phishingScore;
        public double PhishingScore
        {
            get => _phishingScore;
            set { _phishingScore = value; OnPropertyChanges(); }
        }

        async Task FetchEmailBody()
        {
            if (_selectedAccount is null || _selectedEmail is null)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            

            if (_selectedEmail.BodyFetched)
            {
                Mouse.OverrideCursor = null;
                await CheckPhishingAsync();
                return;
            }

            bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);
            if (!isValid)
            {
                Mouse.OverrideCursor = null;
                return;
            }

            await _emailService.FetchEmailBodyAsync(_selectedAccount, _selectedEmail);
            await CheckPhishingAsync();
            Mouse.OverrideCursor = null;
        }

        async Task CheckPhishingAsync()
        {
            if (_selectedEmail == null) return;

            var subject = _selectedEmail.MessageParts.Subject ?? "";
            var body = _selectedEmail.MessageParts.Body ?? "";

            var result = await _phishingService.CheckAsync(subject, body);

            IsPhishing = result.Is_Phishing;
            PhishingScore = result.Score;
        }

        #endregion

        #region Filter
        private CancellationTokenSource? _filterCts; // cancellation when filtering
        public ObservableCollection<Email> FilteredEmails { get; private set; }
        public EmailFilterOptions Filters { get; } = new();
        #endregion

        #region Paging
        private int _pageSize = 50; // emails per page
        private int _currentPage = 0; // 0-based
        private int _totalPages = 0;

        public int CurrentPage => _currentPage + 1;
        public int TotalPages => _totalPages;
        #endregion

        #region Commands
        public RelayCommand AddAccountCommand { get; set; }
        public RelayCommand ComposeCommand { get; set; }
        public RelayCommand ReplyCommand { get; set; }
        public RelayCommand RemoveAccountCommand { get; set; }
        public RelayCommand NextCommand { get; set; }
        public RelayCommand PreviousCommand { get; set; }
        #endregion

        public MainViewModel(
            IAccountService accountService,
            IWindowFactory windowFactory,
            IEmailService emailService,
            IEmailFilterService emailFilterService,
            IPhishingDetectionService phishingService)
        {
            _accountService = accountService;   
            _windowFactory = windowFactory;
            _emailService = emailService;
            _filterService = emailFilterService;
            _phishingService = phishingService;

            FilteredEmails = [];
            Filters.PropertyChanged += async (s, e) => await UpdateFilteredEmailsAsync();

            Accounts = _accountService.GetAccounts();
            SelectedAccount = Accounts.First();

            AddAccountCommand = new RelayCommand(async _ =>
            {
                await _accountService.AddAccountAsync(Provider.Google);
            });

            ComposeCommand = new RelayCommand(_ =>
            {
                var newEmailWindow = _windowFactory.CreateWindow<NewEmailView, NewEmailViewModel>();
                bool? sucess = newEmailWindow.ShowDialog();
                if (sucess == true)
                    MessageBoxHelper.Info("Message was sent");
            }, _ => Accounts.Count > 0);

            ReplyCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null || SelectedEmail == null) return;

                var newEmailWindow = _windowFactory.CreateWindow<NewEmailView, NewEmailViewModel>();
                if (newEmailWindow.DataContext is not NewEmailViewModel vm) return;

                vm.SetupReply(SelectedAccount, SelectedEmail);
                bool? sucess = newEmailWindow.ShowDialog();
                if (sucess == true)
                    MessageBoxHelper.Info("Message was sent");
            }, _ => SelectedAccount is not null && SelectedEmail is not null);

            RemoveAccountCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null) return;

                var result = MessageBoxHelper.Confirmation(
                    $"Are you sure to remove {SelectedAccount.Email}?");

                if (result != true) return;

                _accountService.RemoveAccountAsync(SelectedAccount);
                SelectedAccount = null;
            }, _ => SelectedAccount is not null);

            NextCommand = new RelayCommand(async _ =>
            {
                if (SelectedAccount is null || SelectedAccount.NoMoreOlderEmail) return;

                bool atLastPage = (_currentPage + 1 >= _totalPages);
                if (atLastPage)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    try
                    {
                        var gotMore = await _emailService.FetchOlderHeadersAsync(
                            SelectedAccount, _pageSize);

                        await UpdateFilteredEmailsAsync();
                        if (!gotMore || _currentPage + 1 >= _totalPages) return;
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }
                }

                _currentPage++;
                await UpdateFilteredEmailsAsync();
            });

            PreviousCommand = new RelayCommand(async _ =>
            {
                if (_currentPage <= 0) return;
                _currentPage--;
                await UpdateFilteredEmailsAsync();
            });
        }

        public async Task UpdateFilteredEmailsAsync()
        {
            if (SelectedAccount == null)
            {
                FilteredEmails.Clear();
                _totalPages = 0;
                OnPropertyChanges(nameof(CurrentPage));
                OnPropertyChanges(nameof(TotalPages));
                return;
            }

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
                        allFiltered.Add(email);
                }

                allFiltered = [.. allFiltered.OrderByDescending(e => e.MessageParts.Date)];

                _totalPages = allFiltered.Count == 0
                    ? 0
                    : (int)Math.Ceiling(allFiltered.Count / (double)_pageSize);

                if (_currentPage >= _totalPages)
                    _currentPage = Math.Max(0, _totalPages - 1);

                var pageItems = allFiltered
                    .Skip(_currentPage * _pageSize)
                    .Take(_pageSize);

                FilteredEmails.Clear();
                foreach (var email in pageItems)
                    FilteredEmails.Add(email);

                OnPropertyChanges(nameof(CurrentPage));
                OnPropertyChanges(nameof(TotalPages));
            }
            catch (OperationCanceledException) { }
        }

#pragma warning disable CS8618
        public MainViewModel() { }
#pragma warning restore CS8618
    }
}
