using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;

namespace EmailClientPluma.MVVM.ViewModels;


internal class NewEmailViewModel : ObserableObject, IRequestClose
{
    private readonly List<IEmailService> _emailServices;

    public AppTheme CurrentAppTheme => AppSettings.CurrentTheme;

    private IEmailService GetEmailServiceByProvider(Provider prod)
    {
        var service = _emailServices.FirstOrDefault(x => x.GetProvider().Equals(prod));
        return service ?? throw new NotImplementedException("This service is not implemented for this provider");
    }

    public NewEmailViewModel(IAccountService accountService, IEnumerable<IEmailService> emailServices)
    {
        _emailServices = [.. emailServices];
        Attachments = [];
        Accounts = accountService.GetAccounts();

        SendCommand = new RelayCommandAsync(async _ =>
        {
            if (string.IsNullOrEmpty(Body) && Attachments.Count == 0) return;
            if (SelectedAccount is null) return;
            if (string.IsNullOrEmpty(ToAddresses)) return;
            if (string.IsNullOrEmpty(Subject)) return;

            if(ToAddresses == SelectedAccount.Email)
            {
                MessageBoxHelper.Error("Địa chỉ người nhận không được trùng với địa chỉ người gửi");
                return;
            }

            if (Attachments.Sum(x => x.SizeBytes) > 20_000_000)
            {
                MessageBoxHelper.Error("Tổng size các attachment phải nhỏ hơn 20MB");
                return;
            }

            var email = new Email.OutgoingEmail
            {
                Subject = Subject,
                Body = Body,
                ReplyTo = _replyTo,
                From = SelectedAccount.Email,
                To = ToAddresses,
                Date = DateTime.Now,
                InReplyTo = _inReplyTo,
                Attachments = Attachments,
            };


            try
            {
                var validated = await accountService.ValidateAccountAsync(SelectedAccount);
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

        AddAttachmentCommand = new RelayCommand(_ =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select attachments",
                Multiselect = true,
                CheckFileExists = true,
                CheckPathExists = true
            };
            if (dialog.ShowDialog() is not true) return;


            foreach (var path in dialog.FileNames)
            {
                // Avoid duplicates
                if (Attachments.Any(a =>
                        string.Equals(a.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                FileInfo fi;
                try
                {
                    fi = new FileInfo(path);
                    if (!fi.Exists) continue;
                }
                catch
                {
                    // path invalid / permission issues
                    continue;
                }

                var fileName = fi.Name;
                var contentType = GuessContentType(fi.Extension);

                Attachments.Add(new Attachment
                {
                    FilePath = path,
                    FileName = fileName,
                    ContentType = contentType,
                    SizeBytes = fi.Length,
                });
            }

            RecalculateSize();
        });

        RemoveSelectedAttachmentCommand = new RelayCommand(_ =>
        {
            if (SelectedAttachment is null) return;
            Attachments.Remove(SelectedAttachment);
            RecalculateSize();
        }, _ => SelectedAttachment is not null);

    }

    private void RecalculateSize()
    {
        var totalSize = Attachments.Sum(x => x.SizeBytes);
        TotalAttachmentSizeText = $"{Math.Round(totalSize / 1_000_000f, 2)}MB";
        AttachmentsCountText = Attachments.Count.ToString();

        OnPropertyChanges(nameof(TotalAttachmentSizeText));
        OnPropertyChanges(nameof(AttachmentsCountText));
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


    #region Accounts
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
    #endregion

    #region Commands
    public RelayCommandAsync SendCommand { get; set; }
    public RelayCommand AddAttachmentCommand { get; set; }
    public RelayCommand RemoveSelectedAttachmentCommand { get; set; }


    #endregion

    #region Email Body

    private string? _inReplyTo;
    private bool _isEnable = true;
    private string? _replyTo;
    private Email? _replyToEmail;
    private Account? _selectedAccount;
    private string? _subject;
    private string? _toAddresses;

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
            OnPropertyChanges(nameof(IsNotEnable));
        }
    }

    public bool IsNotEnable
    {
        get => !_isEnable;
        set
        {
            _isEnable = !value;
            OnPropertyChanges();
            OnPropertyChanges(nameof(IsEnable));
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

    #endregion

    #region  Attachments

    public ObservableCollection<Attachment> Attachments { get; set; }
    private Attachment? _selectedAttachment;
    public Attachment? SelectedAttachment
    {
        get => _selectedAttachment;
        set
        {
            _selectedAttachment = value;
            OnPropertyChanges();
        }
    }

    public string AttachmentsCountText { get; set; } = "0";
    public string TotalAttachmentSizeText { get; set; } = "0";

    #endregion



    // This should not be matter because this is for UI type hinting
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public NewEmailViewModel()
    {
    }

    private static string GuessContentType(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext)) return "application/octet-stream";
        ext = ext.Trim().ToLowerInvariant();
        if (!ext.StartsWith(".")) ext = "." + ext;

        return ext switch
        {
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".zip" => "application/zip",
            ".rar" => "application/vnd.rar",
            ".7z" => "application/x-7z-compressed",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };
    }

}