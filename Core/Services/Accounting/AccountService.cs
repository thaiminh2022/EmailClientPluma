using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.Core.Services.Storaging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace EmailClientPluma.Core.Services.Accounting;

internal interface IAccountService
{
    Task AddAccountAsync(Provider provider);
    Task RemoveAccountAsync(Account account);
    Task<bool> ValidateAccountAsync(Account acc);
    ObservableCollection<Account> GetAccounts();
}

/// <summary>
///     This handle reading accounts from database, and using it for UI that needs it.
///     This also helps with monitoring new emails.
/// </summary>
internal class AccountService : IAccountService
{
    private readonly ObservableCollection<Account> _accounts;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _accountValidationLocks;
    private readonly List<IAuthenticationService> _authServices;

    private readonly IEmailMonitoringService _emailMonitoringService;
    private readonly List<IEmailService> _emailServices;
    private readonly IStorageService _storageService;

    public AccountService(
        IEnumerable<IAuthenticationService> authServices,
        IStorageService storageService,
        IEnumerable<IEmailService> emailServices,
        IEmailMonitoringService emailMonitoringService
    )
    {
        _authServices = [.. authServices];
        _emailServices = [.. emailServices];
        _storageService = storageService;
        _emailMonitoringService = emailMonitoringService;
        _accounts = [];
        _accountValidationLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        _ = Initialize();
    }

    /// <summary>
    ///     Add a new account
    /// </summary>
    /// <param name="provider">the type of provider the new account use</param>
    public async Task AddAccountAsync(Provider provider)
    {
        var res = await GetAuthServiceByProvider(provider).AuthenticateAsync();
        if (res is null)
            return;

        // Check if account already exists
        var haveAcc = _accounts.Any(v => v.Email == res.Email);

        if (haveAcc)
        {
            MessageBoxHelper.Info("This email is already signin, ignoring");
            return;
        }

        // If not, add it to database
        var acc = new Account(res);
        await _storageService.StoreAccountAsync(acc);


        // mail not fetched yet
        _accounts.Add(acc);

        // fetching them emails header
        await GetEmailServiceByProvider(acc.Provider).FetchEmailHeaderAsync(acc);

        //start monitoring new email
        if (await ValidateAccountAsync(acc)) _emailMonitoringService.StartMonitor(acc);
    }

    /// <summary>
    ///     Get all the added accounts
    /// </summary>
    /// <returns>An observable collection contains all accounts</returns>
    public ObservableCollection<Account> GetAccounts()
    {
        return _accounts;
    }

    public async Task<List<EmailLabel>> GetLabels(Account acc)
    {
        return (await _storageService.GetLabelsAsync(acc)).ToList();
    }

    /// <summary>
    ///     Check if account is valid, (not expired)
    /// </summary>
    /// <param name="acc">The account to check</param>
    /// <returns>true if valid else false</returns>
    public async Task<bool> ValidateAccountAsync(Account acc)
    {
        if (string.IsNullOrEmpty(acc.ProviderUID))
            return false;

        var validationLock = _accountValidationLocks.GetOrAdd(acc.ProviderUID, _ => new SemaphoreSlim(1, 1));
        await validationLock.WaitAsync();

        try
        {
            return await GetAuthServiceByProvider(acc.Provider).ValidateAsync(acc);
        }
        finally
        {
            validationLock.Release();
        }
    }

    public async Task RemoveAccountAsync(Account account)
    {
        _emailMonitoringService.StopMonitor(account);
        _accounts.Remove(account);
        if (GetAuthServiceByProvider(account.Provider) is IMicrosoftClientApp iClient)
        {
            await iClient.SignOutAsync(account);
        }

        await _storageService.RemoveAccountAsync(account);

    }

    // Call the storage service to get all the saved account
    private async Task Initialize()
    {
        try
        {
            var accounts = await _storageService.GetAccountsAsync();
            foreach (var acc in accounts)
            {
                var emails = await _storageService.GetEmailsAsync(acc);
                acc.Emails = new ObservableCollection<Email>(emails);

                var labels = await _storageService.GetLabelsAsync(acc);
                acc.OwnedLabels = new ObservableCollection<EmailLabel>(labels);

                _accounts.Add(acc);

                if (await ValidateAccountAsync(acc)) _emailMonitoringService.StartMonitor(acc);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Account init exception", innerException: ex);
        }
    }


    private IAuthenticationService GetAuthServiceByProvider(Provider provider)
    {
        var service = _authServices.Find(p => p.GetProvider().Equals(provider));
        return service ?? throw new NotImplementedException("Provider not exists");
    }
    private IEmailService GetEmailServiceByProvider(Provider provider)
    {
        var service = _emailServices.Find(p => p.GetProvider().Equals(provider));
        return service ?? throw new NotImplementedException("Provider not exists");
    }
}