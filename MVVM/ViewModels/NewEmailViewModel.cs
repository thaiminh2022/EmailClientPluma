using System.Collections.ObjectModel;
using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;

namespace EmailClientPluma.MVVM.ViewModels;

internal class NewEmailViewModel : ObserableObject, IRequestClose
{
    private readonly IAccountService _accountService;
    private readonly List<IEmailService> _emailServices;
    private string? _inReplyTo;
    private bool _isEnable = true;
    private string? _replyTo;
    private Email? _replyToEmail;
    private Account? _selectedAccount;
    private string? _subject;
    private string? _toAddresses;


    private IEmailService GetEmailServiceByProvider(Provider prod)
    {
        var service = _emailServices.FirstOrDefault(x => x.GetProvider().Equals(prod));
        if (service is null)
        {
            throw new NotImplementedException("This service is not implemented for this provider");
        }

        return service;
    }

    public NewEmailViewModel(IAccountService accountService, IEnumerable<IEmailService> emailServices)
    {
        _accountService = accountService;
        _emailServices = [..emailServices];
        Accounts = _accountService.GetAccounts();

        SendCommand = new RelayCommand(async _ =>
        {
            if (string.IsNullOrEmpty(Body)) return;
            if (SelectedAccount is null) return;
            if (string.IsNullOrEmpty(ToAddresses)) return;
            if (string.IsNullOrEmpty(Subject)) return;


            var email = new Email.OutgoingEmail
            {
                Subject = Subject,
                Body = Body,
                ReplyTo = _replyTo,
                From = SelectedAccount.Email,
                To = ToAddresses,
                Date = DateTime.Now,
                InReplyTo = _inReplyTo
            };

            try
            {
                var validated = await _accountService.ValidateAccountAsync(SelectedAccount);
                if (validated)
                {
                    await GetEmailServiceByProvider(SelectedAccount.Provider).SendEmailAsync(SelectedAccount, email);
                    RequestClose?.Invoke(this, true);
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Error(ex.Message);
                RequestClose?.Invoke(this, false);
            }
        });

        CancelCommand = new RelayCommand(_ => { RequestClose?.Invoke(this, null); });
    }

    public ObservableCollection<Account> Accounts { get; set; }

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            _selectedAccount = value;
            OnPropertyChanges();
        }
    }

    public RelayCommand SendCommand { get; set; }
    public RelayCommand CancelCommand { get; set; }


    public string? ToAddresses
    {
        get => _toAddresses;
        set
        {
            _toAddresses = value;
            OnPropertyChanges();
        }
    }

    public string? Subject
    {
        get => _subject;
        set
        {
            _subject = value;
            OnPropertyChanges();
        }
    }

    public string? Body { get; set; }

    public bool IsEnable
    {
        get => _isEnable;
        set
        {
            _isEnable = value;
            OnPropertyChanges();
            OnPropertyChanges("IsNotEnable");
        }
    }

    public bool IsNotEnable
    {
        get => !_isEnable;
        set
        {
            _isEnable = !value;
            OnPropertyChanges();
            OnPropertyChanges("IsEnable");
        }
    }

    public Email? ReplyToEmail
    {
        get => _replyToEmail;
        set
        {
            _replyToEmail = value;
            OnPropertyChanges();
        }
    }

    public event EventHandler<bool?>? RequestClose;

    public void SetupReply(Account acc, Email email)
    {
        SelectedAccount = acc;
        ToAddresses = email.MessageParts.From;
        Subject = $"Re: {email.MessageParts.Subject}";
        _inReplyTo = email.MessageIdentifiers.ProviderMessageId;
        _replyTo = email.MessageParts.From;
        IsEnable = false;
        ReplyToEmail = email;
    }

    // This should not be matter because this is for UI type hinting
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public NewEmailViewModel()
    {
    }
}