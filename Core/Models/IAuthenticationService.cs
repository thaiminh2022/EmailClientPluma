using EmailClientPluma.Core.Services;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Windows;

namespace EmailClientPluma.Core.Models
{
    /// <summary>
    /// Interface for all the authentication services (Google, Microsoft, Yahoo)
    /// </summary>
    interface IAuthenticationService
    {
        Provider GetProvider();

        Task<Account?> AuthenticateAsync();
        Task<bool> ValidateAsync(Account acc);
    }
}
