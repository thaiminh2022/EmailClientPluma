using EmailClientPluma.Core.Models;

using System.Collections.ObjectModel;


namespace EmailClientPluma.Core.Services
{
    enum Provider
    {
        Google,
    }
    interface IAccountService
    {
        Task AddAccountAsync(Provider prodiver);
        Task<bool> ValidateAccountAsync(Account? acc);
        ObservableCollection<Account> GetAccounts();
    }

    /// <summary>
    /// This handle reading accounts from database, and using it for UI that needs it
    /// </summary>
    internal class AccountService : IAccountService
    {
        readonly List<IAuthenticationService> _authServices;
        readonly IStorageService _storageService;

        readonly ObservableCollection<Account> _accounts;


        /// <summary>
        /// Helper function, get the first authentication service base on the provider
        /// </summary>
        /// <param name="provider">the provider</param>
        /// <returns>a service for that provider</returns>
        /// <exception cref="NotImplementedException">Cannot find the provider</exception>
        private IAuthenticationService GetAuthServiceByProvider(Provider provider)
        {
            var iAuth = _authServices.Find(p => p.GetProvider().Equals(provider));
            if (iAuth is null) { 
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
            var acc = await GetAuthServiceByProvider(prodiver).AuthenticateAsync();

            if (acc is null)
                return;

            // Check if account already exists
            bool haveAcc = false;
            foreach (var v in _accounts)
            {
                if (v.Email == acc.Email)
                {
                    haveAcc = true;
                    break;
                }
            }
            // If not, add it to database
            if (!haveAcc)
            {
                _accounts.Add(acc);
                await _storageService.StoreAccountAsync(acc);

            }
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
        public async Task<bool> ValidateAccountAsync(Account? acc)
        {
            if (acc is null) 
                return true;

            if (!acc.IsTokenExpired())
                return true;

            bool success = await acc.Credentials.RefreshTokenAsync(CancellationToken.None);
            if (success)
            {
                return true;
            }
            return await GetAuthServiceByProvider(acc.Provider).ValidateAsync(acc);  
        }

        public AccountService(IEnumerable<IAuthenticationService> authServices, IStorageService storageService)
        {
            _authServices = [.. authServices];
            _storageService = storageService;
            _accounts = [];
            var _ = Initialize();
        }

        // Call the storage service to get all the saved account
        async  Task Initialize()
        {
            var accs = await _storageService.GetAccountsAsync();
            foreach (var acc in accs)
            {
                _accounts.Add(acc);
            }
        }
    }
}
