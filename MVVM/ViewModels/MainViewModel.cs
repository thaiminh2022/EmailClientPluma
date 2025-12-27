using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.MVVM.Views;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace EmailClientPluma.MVVM.ViewModels;

internal class MainViewModel : ObserableObject, IRequestClose
{
    public AppTheme CurrentTheme => AppSettings.CurrentTheme;

    private readonly ILogger<MainViewModel> _logger;
    public MainViewModel(IAccountService accountService, IWindowFactory windowFactory,
        IEnumerable<IEmailService> emailServices,
        IEmailFilterService emailFilterService, ILogger<MainViewModel> logger)
    {
        _accountService = accountService;
        _emailServices = [.. emailServices];
        _filterService = emailFilterService;
        _logger = logger;

        SettingsView.DarkModeChanged += (_, _) =>
        {
            OnPropertyChanges(nameof(CurrentTheme));
        };


        // make list auto sort descending by date
        FilteredEmails = [];
        Filters.PropertyChanged += async (s, e) => await UpdateFilteredEmailsAsync();

        Accounts = _accountService.GetAccounts();
        SelectedAccount = Accounts.FirstOrDefault();

        // COMMANDS
        ComposeCommand = new RelayCommand(_ =>
        {
            var newEmailWindow = windowFactory.CreateWindow<NewEmailView, NewEmailViewModel>();
            var success = newEmailWindow.ShowDialog();

            if (success is null)
                return;

            if (success == true) MessageBoxHelper.Info("Message was sent");
        }, _ => Accounts.Count >= 0);

        ReplyCommand = new RelayCommand(_ =>
        {
            if (SelectedAccount == null || SelectedEmail == null) return;
            var newEmailWindow = windowFactory.CreateWindow<NewEmailView, NewEmailViewModel>();

            if (newEmailWindow.DataContext is not NewEmailViewModel vm) return;
            vm.SetupReply(SelectedAccount, SelectedEmail);

            var success = newEmailWindow.ShowDialog();
            if (success is null)
                return;

            if (success == true) MessageBoxHelper.Info("Message was sent");
        }, _ => SelectedAccount is not null && SelectedEmail is not null &&
                SelectedEmail.MessageParts.From != SelectedAccount.Email);

        SettingCommand = new RelayCommand(_ =>
        {
            var newEmailWindow = windowFactory.CreateWindow<SettingsView, SettingsViewModel>();
            newEmailWindow.ShowDialog();
        });

        WhichProvCmd = new RelayCommand(_ =>
        {
            var whichProvWindow = windowFactory.CreateWindow<WhichProvView, WhichProvViewModel>();
            whichProvWindow.ShowDialog();
        });

        RemoveAccountCommand = new RelayCommandAsync(async _ =>
        {
            if (SelectedAccount == null) return;

            var result = MessageBoxHelper.Confirmation($"Are you sure to remove {SelectedAccount.Email}?");
            if (result is null or false) return;

            await _accountService.RemoveAccountAsync(SelectedAccount);

            SelectedAccount = null;

            if (Accounts.Count == 0)
            {
                var chooseLogin = windowFactory.CreateWindow<StartView, StartViewModel>();
                chooseLogin.Show();
                Application.Current.MainWindow = chooseLogin;
                RequestClose?.Invoke(this, true);
            }
        }, _ => SelectedAccount is not null);


        NextCommand = new RelayCommandAsync(async _ =>
        {
            if (SelectedAccount is null || SelectedAccount.NoMoreOlderEmail)
                return;
            var atLastPage = _currentPage + 1 >= TotalPages;

            if (atLastPage)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    var emailService = GetEmailService(SelectedAccount.Provider);
                    var gotMore = await emailService.FetchOlderHeadersAsync(SelectedAccount, _pageSize);

                    // Rebuild pages after loading older headers
                    await UpdateFilteredEmailsAsync();
                    // If we still have no next page (no more server mails), just stop
                    if (!gotMore || _currentPage + 1 >= TotalPages)
                        return;
                }
                catch (Exception ex)
                {
                    MessageBoxHelper.Error(ex.Message);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }

            _currentPage++;
            await UpdateFilteredEmailsAsync();
        }, _ => SelectedAccount is not null && !SelectedAccount.NoMoreOlderEmail);

        PreviousCommand = new RelayCommandAsync(async _ =>
        {
            if (_currentPage <= 0) return;
            _currentPage--;
            await UpdateFilteredEmailsAsync();
        }, _ => SelectedAccount is not null && _currentPage > 0);

        NewLabelCommand = new RelayCommand(_ =>
        {
            // OPEN NEW LABEL DIALOG

            var labelEditorWindow = windowFactory.CreateWindow<LabelEditorView, LabelEditorViewModel>();
            if (labelEditorWindow.DataContext is not LabelEditorViewModel vm) return;
            vm.SelectedAccount = SelectedAccount;
            labelEditorWindow.ShowDialog();

        }, _ => _selectedAccount is not null);

        EditEmailLabelCommand = new RelayCommand(_ =>
        {
            if (SelectedAccount is null || SelectedEmail is null) return;

            var emailLabelEditorWindow = windowFactory.CreateWindow<EmailLabelEditView, EmailLabelEditViewModel>();

            if (emailLabelEditorWindow.DataContext is EmailLabelEditViewModel vm)
                vm.Setup(SelectedAccount, SelectedEmail);

            emailLabelEditorWindow.ShowDialog();
        }, _ => SelectedEmail is not null && SelectedAccount is not null);

        RefreshEmailCommand = new RelayCommandAsync(async _ =>
        {
            if (SelectedAccount is null)
                return;
            try
            {
                var emailService = GetEmailService(SelectedAccount.Provider);
                await emailService.FetchEmailHeaderAsync(SelectedAccount);
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Error(ex.Message);
            }

        }, _ => SelectedAccount is not null);

        OpenAttachmentCommand = new RelayCommand(async _ =>
        {
            if (_selectedAttachment is null || !_selectedAttachment.ContentFetched) return;

            if (!File.Exists(_selectedAttachment.FilePath))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_selectedAttachment.FilePath}\"",
                UseShellExecute = true
            });
        }, _ => _selectedAttachment is not null);
    }

    // This should not be matter because this is for UI type hinting
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public MainViewModel()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
    }

    private async Task UpdateFilteredEmailsAsync()
    {
        if (SelectedAccount == null)
        {
            FilteredEmails.Clear();

            TotalPages = 0;
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
                if (await _filterService.MatchFiltersAsync(email, Filters, token))
                    allFiltered.Add(email);

            // 2. Sort (newest first) if you want
            allFiltered = [.. allFiltered.OrderByDescending(e => e.MessageParts.Date)];

            // 3. Compute total pages
            if (allFiltered.Count == 0)
                TotalPages = 0;
            else
                TotalPages = (int)Math.Ceiling(allFiltered.Count / (double)_pageSize);

            if (_currentPage >= TotalPages)
                _currentPage = Math.Max(0, TotalPages - 1);

            // 4. Slice current page
            var pageItems = allFiltered
                .Skip(_currentPage * _pageSize)
                .Take(_pageSize);

            FilteredEmails.Clear();
            foreach (var email in pageItems) FilteredEmails.Add(email);

            OnPropertyChanges(nameof(CurrentPage));
            OnPropertyChanges(nameof(TotalPages));

            CommandManager.InvalidateRequerySuggested();
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Filter operation canceled, this should be normal behavior");
            // normal exit
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "ERROR WHILE FILTERING EMAIL");
            MessageBoxHelper.Error("Error while filtering email: ", ex.Message);
        }
    }

    #region Services

    private readonly IAccountService _accountService;
    private readonly List<IEmailService> _emailServices;
    private readonly IEmailFilterService _filterService;


    private IEmailService GetEmailService(Provider prod)
    {
        var service = _emailServices.Find(x => x.GetProvider() == prod);
        return service ?? throw new NotImplementedException("Service not implemented");
    }

    #endregion

    #region Accounts

    // A list of login account
    public ObservableCollection<Account> Accounts { get; set; }
    private Account? _selectedAccount;

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (_selectedAccount == value) return;

            if (_selectedAccount is not null)
                _selectedAccount.Emails.CollectionChanged -= Emails_CollectionChanged;

            _selectedAccount = value;
            OnPropertyChanges();

            FilteredEmails.Clear();

            _currentPage = 0;
            CommandManager.InvalidateRequerySuggested();

            if (_selectedAccount is null) return;
            _selectedAccount.Emails.CollectionChanged += Emails_CollectionChanged;

            Filters.SelectedLabel = EmailLabel.Inbox;


            // Initial fill
            _ = UpdateFilteredEmailsAsync();
            _ = FetchNewHeadersAndPrefetchBody();
        }
    }


    private async void Emails_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            await UpdateFilteredEmailsAsync();
        }
        catch (Exception ex)
        {
            MessageBoxHelper.Error("Error when updating filtered email\n", ex);
        }
    }

    private async Task FetchNewHeadersAndPrefetchBody()
    {
        if (_selectedAccount is null)
            return;
        try
        {
            var isValid = await _accountService.ValidateAccountAsync(_selectedAccount);

            if (!isValid || _selectedAccount.FirstTimeHeaderFetched)
            {
                return;
            }

            await GetEmailService(_selectedAccount.Provider).FetchEmailHeaderAsync(_selectedAccount);
            _selectedAccount.FirstTimeHeaderFetched = true;
            await GetEmailService(_selectedAccount.Provider).PrefetchRecentBodiesAsync(_selectedAccount);
        }
        catch (Exception ex)
        {
            MessageBoxHelper.Error(ex.Message);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

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

            _ = FetchEmailBody();
            OnPropertyChanges();
        }
    }

    private async Task FetchEmailBody()
    {
        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            if (_selectedAccount is null || _selectedEmail is null)
                return;


            if (!_selectedEmail.BodyFetched)
            {
                var isValid = await _accountService.ValidateAccountAsync(_selectedAccount);
                if (!isValid) return;

                var emailService = GetEmailService(_selectedAccount.Provider);
                await emailService.FetchEmailBodyAsync(_selectedAccount, _selectedEmail);
            }

            CheckPhishing();
        }
        catch (Exception ex)
        {
            MessageBoxHelper.Error(ex.Message);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        return;
        void CheckPhishing()
        {
            if (!AppSettings.UsePhishingDetector) return;

            var check = PhishDetector.ValidateHtmlContent(_selectedEmail?.MessageParts.Body ?? "");
            if (check is PhishDetector.SuspiciousLevel.None or PhishDetector.SuspiciousLevel.Minor) return;
            MessageBoxHelper.Warning("Cảnh báo phishing: ", check.ToString());
        }
    }

    private Attachment? _selectedAttachment;

    public Attachment? SelectedAttachment
    {
        get => _selectedAttachment;
        set
        {
            _selectedAttachment = value;
            Mouse.OverrideCursor = Cursors.Wait;
            _ = FetchEmailAttachment();
        }
    }

    public async Task FetchEmailAttachment()
    {
        if (_selectedAttachment is null || _selectedAccount is null || _selectedEmail is null) return;
        if (_selectedAttachment.ContentFetched) return;
        var emailService = GetEmailService(_selectedAccount.Provider);
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            await emailService.FetchEmailAttachmentsAsync(_selectedAccount, _selectedEmail);

        }
        catch (Exception ex)
        {
            MessageBoxHelper.Error(ex.Message);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    #endregion

    #region Filter

    private CancellationTokenSource? _filterCts; //cancellation when filtering
    public ObservableCollection<Email> FilteredEmails { get; set; }
    public EmailFilterOptions Filters { get; } = new(); // Filter options

    #endregion

    #region Paging

    private readonly int _pageSize = 50; // emails per page
    private int _currentPage; // 0-based

    public int CurrentPage => _currentPage + 1; // expose as 1-based for UI

    public int TotalPages { get; private set; }

    #endregion

    #region Commands

    public RelayCommandAsync RefreshEmailCommand { get; set; }

    public RelayCommand SettingCommand { get; set; }
    public RelayCommand WhichProvCmd { get; set; }
    public RelayCommand ComposeCommand { get; set; }
    public RelayCommand ReplyCommand { get; set; }
    public RelayCommandAsync RemoveAccountCommand { get; set; }
    public RelayCommandAsync NextCommand { get; set; }
    public RelayCommandAsync PreviousCommand { get; set; }

    public RelayCommand NewLabelCommand { get; set; }
    public RelayCommand EditEmailLabelCommand { get; set; }

    public RelayCommand OpenAttachmentCommand{ get; set; }

    #endregion

    public event EventHandler<bool?>? RequestClose;
}