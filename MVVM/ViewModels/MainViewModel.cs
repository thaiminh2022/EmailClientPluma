using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.MVVM.Views;
using MailKit;
using MailKit.Search;
using MimeKit;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using static EmailClientPluma.Core.Models.Email;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class MainViewModel : ObserableObject
    {
        readonly IAccountService _accountService;
        readonly IEmailService _emailService;
        readonly IWindowFactory _windowFactory;
        readonly IEmailFilterService _filterService;

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
                OnPropertyChanges();
                UpdateFilteredEmailsAsync();
            }
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
                MessageBox.Show(_selectedEmail.MessageParts.From);
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
            ;

            bool isValid = await _accountService.ValidateAccountAsync(_selectedAccount);

            if (!isValid)
            {
                Mouse.OverrideCursor = null;
            }

            await _emailService.FetchEmailBodyAsync(_selectedAccount, _selectedEmail);
            Mouse.OverrideCursor = null;

        }




        public RelayCommand AddAccountCommand { get; set; }
        public RelayCommand ComposeCommand { get; set; }
        public RelayCommand ReplyCommand { get; set; }
        public RelayCommand RemoveAccountCommand { get; set; }

        public MainViewModel(IAccountService accountService, IWindowFactory windowFactory, IEmailService emailService,IEmailFilterService emailFilterService)
        {
            _accountService = accountService;
            _windowFactory = windowFactory;
            _emailService = emailService;
            _filterService = emailFilterService;

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
        #region Filter Property Bindings
        public string From
        {
            get => Filters.From;
            set { Filters.From = value; _ = UpdateFilteredEmailsAsync(); }
        }

        public string To
        {
            get => Filters.To;
            set { Filters.To = value; _ = UpdateFilteredEmailsAsync(); }
        }

        public string Subject
        {
            get => Filters.Subject;
            set { Filters.Subject = value; _ = UpdateFilteredEmailsAsync(); }
        }

        public string HasWords
        {
            get => Filters.HasWords;
            set { Filters.HasWords = value; _ = UpdateFilteredEmailsAsync(); }
        }

        public string DoesNotHave
        {
            get => Filters.DoesNotHave;
            set { Filters.DoesNotHave = value; _ = UpdateFilteredEmailsAsync(); }
        }

        public DateTime? SelectedDate
        {
            get => Filters.SelectedDate;
            set { Filters.SelectedDate = value; _ = UpdateFilteredEmailsAsync(); }
        }

        // Range selected in ComboBox (1 = 1 day, 2 = 1 week, 3 = 1 month)
        public short DateRangeIndex
        {
            get => Filters.DateRangeIndex;
            set { Filters.DateRangeIndex = value; _ = UpdateFilteredEmailsAsync(); }
        }


        public string SearchText
        {
            get => Filters.SearchText;
            set { Filters.SearchText = value; _ = UpdateFilteredEmailsAsync(); }
        }

        public int MailboxIndex
        {
            get => Filters.MailboxIndex;
            set { Filters.MailboxIndex = value; _ = UpdateFilteredEmailsAsync(); }
        }

        public bool HasAttachment
        {
            get => Filters.HasAttachment;
            set { Filters.HasAttachment = value; _ = UpdateFilteredEmailsAsync(); }
        }




        #endregion








        public MainViewModel()
        {
        }
    }
}