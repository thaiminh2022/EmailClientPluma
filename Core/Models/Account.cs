using EmailClientPluma.Core.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util;

namespace EmailClientPluma.Core.Models
{
    /// <summary>
    /// Store account infos lol
    /// </summary>
    internal record Account
    {
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public Provider Provider { get; set; }
        public UserCredential Credentials { get; set; }
        public Account(string displayName, Provider provider, string email, UserCredential credentials)
        {
            Provider = provider;
            DisplayName = displayName;
            Email = email;
            Credentials = credentials;
        }

        public bool IsTokenExpired()
        {
            return Credentials.Token.IsStale;
        }
    }
}
