using EmailClientPluma.Core.Models;

using System.Collections.ObjectModel;
using System.Windows;


namespace EmailClientPluma.Core.Services
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

        Task StartMonitoringAsync(Account acc);
        void StopMonitoringAsync(Account acc);

    }

    /// <summary>
    /// This handle reading accounts from database, and using it for UI that needs it
    /// </summary>
    internal class AccountService : IAccountService
    {
        readonly List<IAuthenticationService> _authServices;
        readonly IStorageService _storageService;
        readonly IEmailMonitoringService _emailMonitoringService;
        readonly ObservableCollection<Account> _accounts;

        public AccountService(
            IEnumerable<IAuthenticationService> authServices, 
            IStorageService storageService,
            IEmailMonitoringService emailMonitoringService
        )
        {
            _authServices = [.. authServices];
            _storageService = storageService;
            _emailMonitoringService = emailMonitoringService;
            _accounts = [];
            var _ = Initialize();
        }

        // Call the storage service to get all the saved account
        async Task Initialize()
        {
            try
            {
                var accs = await _storageService.GetAccountsAsync();
                foreach (var acc in accs)
                {
                    var emails = await _storageService.GetEmailsAsync(acc);
                    acc.Emails = new(emails);
                    _accounts.Add(acc);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Email ex: " + ex.Message);
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
            AuthResponce? res = await GetAuthServiceByProvider(prodiver).AuthenticateAsync();

            if (res is null)
                return;

            // Check if account already exists
            bool haveAcc = false;
            foreach (var v in _accounts)
            {
                if (v.Email == res.Email)
                {
                    haveAcc = true;
                    break;
                }
            }
            if (haveAcc) return;

            // If not, add it to database
            var acc = new Account(res);
            await _storageService.StoreAccountAsync(acc);

            // mail not fetched yet
            _accounts.Add(acc);
        }
        /// <summary>
        /// Get all the added accounts
        /// </summary>
        /// <returns>An obserable collection contains all accounts</returns>
        public ObservableCollection<Account> GetAccounts()
        {
            return _accounts;
        }

        /// <summary>
        /// Check if account is valid, (not expired)
        /// </summary>
        /// <param name="acc">The account to check</param>
        /// <returns>true if valid else false</returns>
        public async Task<bool> ValidateAccountAsync(Account acc)
        {
            return await GetAuthServiceByProvider(acc.Provider).ValidateAsync(acc);
        }
        public async Task RemoveAccountAsync(Account account)
        {
            _accounts.Remove(account);
            await _storageService.RemoveAccountAsync(account);
        }

        public async Task StartMonitoringAsync(Account acc)
        {
            bool accountValid= await  ValidateAccountAsync(acc);
            if (!accountValid)
                return;
            _emailMonitoringService.StartMonitor(acc);
        }

        public void StopMonitoringAsync(Account acc)
        {
             _emailMonitoringService.StopMonitor(acc);
        }
    }
}
