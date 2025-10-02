using EmailClientPluma.Core.Services;
using Google.Apis.Auth.OAuth2;
using System.Security.Permissions;

namespace EmailClientPluma.Core.Models
{
    /// <summary>
    /// Interface for all the authentication services (Google, Microsoft, Yahoo)
    /// </summary>
    interface IAuthenticationService
    {
        Provider GetProvider();

        Task<AuthResponce?> AuthenticateAsync();
        Task<bool> ValidateAsync(Account acc);
    }

    record AuthResponce
    {
        public string ProviderUID { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public Provider Provider { get; set; }
        public Credentials Credentials { get; set; }

        public AuthResponce(string providerUID, string email, string displayName, Provider provider, Credentials credentials)
        {
            ProviderUID = providerUID;
            Email = email;
            DisplayName = displayName;
            Provider = provider;
            Credentials = credentials;
        }
    }
}
