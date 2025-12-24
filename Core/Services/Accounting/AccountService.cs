using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.Core.Services.Storaging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;


namespace EmailClientPluma.Core.Services.Accounting
{
    enum Provider
    {
        Google,
    }
    interface IAccountService
    {
        Task AddAccountAsync(Provider prodiver);
        Task RemoveAccountAsync(Account account);
        Task<bool> ValidateAccountAsync(Account acc);
        ObservableCollection<Account> GetAccounts();

        Task<List<EmailLabel>> GetLabels(Account acc);

    }

    /// <summary>
    /// This handle reading accounts from database, and using it for UI that needs it.
    /// This also helps with monitoring new emails.
    /// </summary>
    internal class AccountService : IAccountService
    {
        readonly List<IAuthenticationService> _authServices;
        readonly IStorageService _storageService;
        readonly IEmailService _emailService;

        readonly IEmailMonitoringService _emailMonitoringService;
        readonly ObservableCollection<Account> _accounts;
        readonly ConcurrentDictionary<string, SemaphoreSlim> _accountValidationLocks;

        public AccountService(
            IEnumerable<IAuthenticationService> authServices,
            IStorageService storageService,
            IEmailService emailService,
            IEmailMonitoringService emailMonitoringService
        )
        {
            _authServices = [.. authServices];
            _emailService = emailService;
            _storageService = storageService;
            _emailMonitoringService = emailMonitoringService;
            _accounts = [];
            _accountValidationLocks = new();

            _ = Initialize();
        }

        // Call the storage service to get all the saved account
        async Task Initialize()
        {
            try
            {
                var accs = await _storageService.GetAccountsAsync().ConfigureAwait(false);
                foreach (var acc in accs)
                {
                    var emails = await _storageService.GetEmailsAsync(acc);
                    acc.Emails = new ObservableCollection<Email>(emails);

                    var labels = await _storageService.GetLabelsAsync(acc);
                    acc.OwnedLabels = new ObservableCollection<EmailLabel>(labels);

                    _accounts.Add(acc);

                    if (await ValidateAccountAsync(acc))
                    {
                        _emailMonitoringService.StartMonitor(acc);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Error("Account initialize exception: ", ex);
            }

        }

        /// <summary>
        /// Helper function, get the first authentication service base on the provider
        /// </summary>
        /// <param name="provider">the provider</param>
        /// <returns>a service for that provider</returns>
        /// <exception cref="NotImplementedException">Cannot find the provider</exception>
        private IAuthenticationService GetAuthServiceByProvider(Provider provider)
        {
            var iAuth = _authServices.Find(p => p.GetProvider().Equals(provider));
            if (iAuth is null)
            {
                throw new NotImplementedException("Provider not exists");
            }
            return iAuth;
        }

        /// <summary>
        /// Add a new account
        /// </summary>
        /// <param name="prodiver">the type of provider the new account use</param>
        /// <returns></returns>
        public async Task AddAccountAsync(Provider prodiver)
        {
            var res = await GetAuthServiceByProvider(prodiver).AuthenticateAsync();

            if (res is null)
                return;

            // Check if account already exists
            var haveAcc = false;
            foreach (var v in _accounts)
            {
                if (v.Email != res.Email) continue;

                haveAcc = true;
                break;
            }
            if (haveAcc) return;

            // If not, add it to database
            var acc = new Account(res);
            await _storageService.StoreAccountAsync(acc);


            // mail not fetched yet
            _accounts.Add(acc);

            // fetching them emails header
            await _emailService.FetchEmailHeaderAsync(acc);



            //start monitoring new email
            if (await ValidateAccountAsync(acc))
            {
                _emailMonitoringService.StartMonitor(acc);
            }

        }
        /// <summary>
        /// Get all the added accounts
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
        /// Check if account is valid, (not expired)
        /// </summary>
        /// <param name="acc">The account to check</param>
        /// <returns>true if valid else false</returns>
        public async Task<bool> ValidateAccountAsync(Account acc)
        {
            ArgumentNullException.ThrowIfNull(acc);
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
            await _storageService.RemoveAccountAsync(account);

        }

    }
}
