using EmailClientPluma.Core.Services;
using Google.Apis.Auth.OAuth2;

namespace EmailClientPluma.Core.Models
{
    /// <summary>
    /// Store account infos lol
    /// </summary>
    internal class Account
    {
        public int AccountID { get; set; } // auto created in database
        public string ProviderUID { get; set; }
        public string Email { get; set; }

        public string DisplayName { get; set; }
        public Provider Provider { get; set; }

        public IEnumerable<Email> Emails { get; set; } = [];

        readonly public UserCredential Credentials;

        public Account(string providerUID, string email, string displayName, Provider provider, UserCredential credentials)
        {
            AccountID = 0;
            ProviderUID = providerUID;
            Email = email;
            DisplayName = displayName;
            Provider = provider;
            Credentials = credentials;
        }

        public bool IsTokenExpired()
        {
            return Credentials.Token.IsStale;
        }
    }
}
