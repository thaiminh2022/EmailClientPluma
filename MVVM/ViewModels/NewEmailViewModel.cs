using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using System.Collections.ObjectModel;
using System.Windows;

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
            get => _selectedAccount;
            set { _selectedAccount = value; OnPropertyChanges(); }
        }

        public string? ToAddresses { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public string? ReplyTo { get; set; }

        public string? InReplyTo { get; set; }
        public string? References { get; set; } = null;

        public RelayCommand SendCommand { get; }


        //property of B
        private readonly bool _isReply;

        // 🟢 Constructor for NEW email
        public NewEmailViewModel(IAccountService accountService, IEmailService emailService)
        {
            _accountService = accountService;
            _emailService = emailService;
            Accounts = _accountService.GetAccounts();

            SendCommand = new RelayCommand(async (_) => await SendAsync());
        }

        // 🟢 Constructor for REPLY email
        public NewEmailViewModel(IAccountService accountService, IEmailService emailService,
                                 Account replyingAccount, Email originalEmail)
            : this(accountService, emailService)
        {
            _isReply = true;
            SelectedAccount = replyingAccount;

            // Fill default values
            ToAddresses = originalEmail.MessageParts.From;
            Subject = originalEmail.MessageParts.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
                ? originalEmail.MessageParts.Subject
                : "Re: " + originalEmail.MessageParts.Subject;

            ReplyTo = originalEmail.MessageParts.From;
            InReplyTo = originalEmail.MessageIdentifiers.MessageID;
            References = string.Join(" ",
                         new[] { originalEmail.MessageIdentifiers.InReplyTo, originalEmail.MessageIdentifiers.MessageID }
                         .Where(s => !string.IsNullOrEmpty(s)));

            // Include quoted original message
            /*
            Body = $"\n\n--- Original message ---\n" +
                   $"From: {originalEmail.MessageParts.From}\n" +
                   $"Date: {originalEmail.MessageParts.Date}\n" +
                   $"Subject: {originalEmail.MessageParts.Subject}\n\n" +
                   $"{originalEmail.MessageParts.Body}";
            */
        }

        private async Task SendAsync()
        {
            if (SelectedAccount is null || string.IsNullOrWhiteSpace(ToAddresses) ||
                string.IsNullOrWhiteSpace(Subject) || string.IsNullOrWhiteSpace(Body))
            {
                MessageBox.Show("Please fill all fields before sending.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var email = new Email.OutgoingEmail
            {
                Subject = Subject!,
                Body = Body!,
                ReplyTo = ReplyTo,
                From = SelectedAccount.Email,
                To = ToAddresses!,
                Date = DateTime.Now,
                References = References,
                InReplyTo = ReplyTo
            };

            try
            {
                var validated = await _accountService.ValidateAccountAsync(SelectedAccount);
                if (validated)
                {
                    await _emailService.SendEmailAsync(SelectedAccount, email);
                    MessageBox.Show(_isReply ? "Reply sent successfully." : "Message sent successfully.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send email: {ex.Message}");
            }
        }

        public NewEmailViewModel() { }
    }
}
