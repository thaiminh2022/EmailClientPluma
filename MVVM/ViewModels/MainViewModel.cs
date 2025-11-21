using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.MVVM.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
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
        public ObservableCollection<Account> Accounts { 
            get; 
            private set; 
        }
        // Account selected in the list view


        private Account? _selectedAccount;
        public Account? SelectedAccount
        {
            get { return _selectedAccount; }
            set
            {
                _selectedAccount = value;
                OnPropertyChanges();

                //Mouse.OverrideCursor = Cursors.Wait;

                //_ = FetchNewHeaders();
                _ = UpdateFilteredEmailsAsync();
            }
        }

        private async Task FetchNewHeaders()
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
                // MessageBox.Show(_selectedEmail.MessageParts.From);


                if (_selectedEmail is not null && _selectedEmail.BodyFetched)
                {
                    var result = PhishDetector.ValidateHtmlContent(_selectedEmail.MessageParts.Body ?? "");
                    MessageBox.Show(result.ToString());
                }

            }
        }

        private CancellationTokenSource? _filterCts; //cancellation when filtering
        public ICollectionView? FilteredEmails { get; private set; }
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

            Accounts = _accountService.GetAccounts();

            //SelectedAccount = Accounts.First();
            Filters.PropertyChanged += async (s, e) => await UpdateFilteredEmailsAsync();



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
                    MessageBox.Show("Message was sent");
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
                }
                ;

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
                FilteredEmails = null;
                OnPropertyChanges(nameof(FilteredEmails));
                return;
            }

            var emails = SelectedAccount.Emails;

            // Cancel previous filter if running
            _filterCts?.Cancel();
            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;

            var filteredList = new List<Email>();
            foreach (var email in emails)
            {
                if (await _filterService.MatchFiltersAsync(email, Filters, token))
                {
                    filteredList.Add(email);
                }
            }

            FilteredEmails = CollectionViewSource.GetDefaultView(filteredList);
            OnPropertyChanges(nameof(FilteredEmails));
        }
        #endregion


        public MainViewModel()
        {
        }
    }
}