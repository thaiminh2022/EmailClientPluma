using EmailClientPluma.Core.Models;
using Google.Apis.Auth.OAuth2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private readonly ObservableCollection<Account> _accounts;    

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

            if (acc is not null)
            {
                _accounts.Add(acc);
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

        public AccountService(IEnumerable<IAuthenticationService> authServices)
        {
            _authServices = [.. authServices];
            _accounts = [];
        }
    }
}
