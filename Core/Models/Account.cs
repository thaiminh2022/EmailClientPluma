using EmailClientPluma.Core.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using System.Net;

namespace EmailClientPluma.Core.Models
{
    /// <summary>
    /// Store account infos lol
    /// </summary>
    internal class Account
    {
        public int AccountID { get; set; }
        public string ProviderID { get; set;}
        public string Email { get; set; }

        public string DisplayName { get; set; }
        public Provider Provider { get; set; }
        
        readonly public UserCredential Credentials;

        public Account(int accountID, string providerID, string email, string displayName, Provider provider, UserCredential credentials)
        {
            AccountID = accountID;
            ProviderID = providerID;
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
