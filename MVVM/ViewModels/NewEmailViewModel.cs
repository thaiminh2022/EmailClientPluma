using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using System.Collections.ObjectModel;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class NewEmailViewModel : ObserableObject
    {
        IAccountService _accountService;
        IEmailService _emailService;

        public ObservableCollection<Account> Accounts { get; set; }
        private Account? _selectedAccount;
        public Account? SelectedAccount
        {
            get { return _selectedAccount; }
            set
            {
                _selectedAccount = value;
                OnPropertyChanges();
            }
        }
        public RelayCommand SendCommand { get; set; }

        public string? ToAddresses { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }


        public NewEmailViewModel(IAccountService accountService, IEmailService emailService)
        {
            _accountService = accountService;
            _emailService = emailService;
            Accounts = _accountService.GetAccounts();

            SendCommand = new RelayCommand(async (_) =>
            {
                if (SelectedAccount is null) return;
                if (string.IsNullOrEmpty(ToAddresses)) return;
                if (string.IsNullOrEmpty(Subject)) return;
                if (string.IsNullOrEmpty(Body)) return;



                //var email = new Email(
                //        SelectedAccount.ProviderUID,
                //        Subject,
                //        Body,
                //        SelectedAccount.Email,
                //        ToAddresses,
                //        []
                //);

                //try
                //{
                //    var validated = await _accountService.ValidateAccountAsync(SelectedAccount);
                //    if (validated)
                //    {
                //        await _emailService.SendEmailAsync(SelectedAccount, email);
                //        MessageBox.Show("Message was sent");
                //    }
                //}
                //catch (Exception ex)
                //{
                //    MessageBox.Show(ex.Message);
                //}

            });
        }


        public NewEmailViewModel() { }
    }
}
