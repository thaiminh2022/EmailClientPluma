using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.MVVM.Views;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        readonly IPhishingDetectionService _phishingService;
        #endregion

        #region Accounts
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
            if (!isValid || _selectedAccount.FirstTimeHeaderFetched) return;

            await _emailService.FetchEmailHeaderAsync(_selectedAccount);
            _selectedAccount.FirstTimeHeaderFetched = true;
            await _emailService.PrefetchRecentBodiesAsync(_selectedAccount, 30);
        }
        #endregion

        #region Emails
        private Email? _selectedEmail;
        public Email? SelectedEmail
        {
            get => _selectedEmail;
            set
            {
                _selectedEmail = value;
                Mouse.OverrideCursor = Cursors.Wait;
                _ = FetchEmailBody();
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

            if (!_selectedEmail.BodyFetched)
            {
                bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);
                if (!isValid) return;

                await _emailService.FetchEmailBodyAsync(_selectedAccount, _selectedEmail);
            }

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

            if (IsPhishing && PhishingScore > 0.8)
            {
                MessageBoxHelper.Warning(
                    $" The email shows signs of phishing.!\nReliability: {(PhishingScore * 100):0.00}%"
                );
            }
        }
        #endregion

        #region Filter
        private CancellationTokenSource? _filterCts;
        public ObservableCollection<Email> FilteredEmails { get; private set; }
        public EmailFilterOptions Filters { get; } = new();
        #endregion

        #region Paging
        private int _pageSize = 50;
        private int _currentPage = 0;
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
            SelectedAccount = Accounts.FirstOrDefault();

            AddAccountCommand = new RelayCommand(async _ =>
            {
                await _accountService.AddAccountAsync(Provider.Google);
            });

            ComposeCommand = new RelayCommand(_ =>
            {
                var win = _windowFactory.CreateWindow<NewEmailView, NewEmailViewModel>();
                if (win.ShowDialog() == true)
                    MessageBoxHelper.Info("Message was sent");
            });
        }

        public async Task UpdateFilteredEmailsAsync()
        {
            if (SelectedAccount == null) return;

            _filterCts?.Cancel();
            _filterCts = new CancellationTokenSource();

            var list = new List<Email>();
            foreach (var email in SelectedAccount.Emails)
            {
                if (await _filterService.MatchFiltersAsync(email, Filters, _filterCts.Token))
                    list.Add(email);
            }

            list = list.OrderByDescending(e => e.MessageParts.Date).ToList();
            _totalPages = (int)Math.Ceiling(list.Count / (double)_pageSize);

            FilteredEmails.Clear();
            foreach (var mail in list.Take(_pageSize))
                FilteredEmails.Add(mail);

            OnPropertyChanges(nameof(CurrentPage));
            OnPropertyChanges(nameof(TotalPages));
        }

#pragma warning disable CS8618
        public MainViewModel() { }
#pragma warning restore CS8618
    }
}
