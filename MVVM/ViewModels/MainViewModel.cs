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
                UpdateFilteredEmails();
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

        public ICollectionView? FilteredEmails { get; private set; } 

        async Task FetchEmailBody()
        {
            if (_selectedAccount is null || _selectedEmail is null || _selectedEmail.BodyFetched)
            {
                Mouse.OverrideCursor = null;
                return;
            };

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
                };

            }, _ => SelectedAccount is not null);

            ReplyCommand = new RelayCommand(_ =>
            {
                if (SelectedAccount == null || SelectedEmail == null) return;
                // too lazy to implement for now

            }, _ => SelectedAccount is not null && SelectedEmail is not null &&
                    SelectedEmail.MessageParts.From != SelectedAccount.Email);
        }

        public void UpdateFilteredEmails()
        {
            if (SelectedAccount == null)
            {
                FilteredEmails = null;
            }
            else
            {
                FilteredEmails = CollectionViewSource.GetDefaultView(SelectedAccount.Emails);
                FilteredEmails.Filter = FilterEmails;
            }

            OnPropertyChanges(nameof(FilteredEmails));
        }


        private string _searchText = "";
        private DateTime _startDate = DateTime.MinValue;
        private DateTime _endDate = DateTime.MaxValue;

        private MailboxAddress _emailSenderFilter = new MailboxAddress("Display Name","newultragame@gmail.com");
     
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                _searchText = value;
                OnPropertyChanges();
                FilteredEmails.Refresh();
            }
        } 
        public DateTime StartDate
        {
            get { return _startDate; }
            set
            {
                _startDate = value;
                OnPropertyChanges();
                FilteredEmails.Refresh();
            }
        }
        public DateTime EndDate
        {
            get { return _endDate; }
            set
            {
                _endDate = value;
                OnPropertyChanges();
                FilteredEmails.Refresh();
            }
        }
        public MailboxAddress EmailSenderFilter
        {
            get { return _emailSenderFilter; }
            set
            {
                _emailSenderFilter = value;
                OnPropertyChanges();
                FilteredEmails.Refresh();
            }
        }





        private bool FilterEmails(object obj)
        {
            if (obj is not Email) return false;
           

            DataParts email = ((Email)obj).MessageParts;
            bool IsSearch = email.Subject.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || email.From.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || email.To.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || (email.Body?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);

            if (string.IsNullOrWhiteSpace(SearchText)) IsSearch = true;

            bool IsDateInRange = email.Date.HasValue && 
                                 email.Date.Value.DateTime >= StartDate && 
                                 email.Date.Value.DateTime <= EndDate;
            //MailboxAddress m =  MailboxAddress.Parse(email.From);
            //bool IsSameSender = m.Address.Equals(EmailSenderFilter.Address, StringComparison.OrdinalIgnoreCase);

            // Sender filter (safer)
            bool IsSameSender = false;
            try
            {
                var parsed = InternetAddressList.Parse(email.From);
                foreach (var addr in parsed.Mailboxes)
                {
                    if (string.Equals(addr.Address?.Trim(), EmailSenderFilter.Address?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        IsSameSender = true;
                        break;
                    }
                }
            }
            catch
            {
                // ignore parsing errors (invalid From)
            }

            
        
            return IsDateInRange && IsSearch && IsSameSender;
        }








        public MainViewModel()
        {
        }
    }
}
