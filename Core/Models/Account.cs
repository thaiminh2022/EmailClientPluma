using EmailClientPluma.Core.Services;

namespace EmailClientPluma.Core.Models
{
    /// <summary>
    /// Store account infos lol
    /// </summary>
    internal class Account
    {
        public string ProviderUID { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public Provider Provider { get; set; }
        public IEnumerable<Email> Emails { get; set; } = [];

        public Credentials Credentials { get; set; }


        public Account(string providerUID, string email, string displayName, Provider provider, Credentials credentials)
        {
            ProviderUID = providerUID;
            Email = email;
            DisplayName = displayName;
            Provider = provider;
            Credentials = credentials;
        }
        public Account(AuthResponce authResponce)
        {
            ProviderUID = authResponce.ProviderUID;
            Email = authResponce.Email;
            DisplayName = authResponce.DisplayName;
            Provider = authResponce.Provider;
            Credentials = authResponce.Credentials;
        }


    }

    public record Credentials
    {
        public string SessionToken { get; set; }
        public string RefreshToken { get; set; }


        public Credentials(string sessionToken, string refreshToken)
        {
            SessionToken = sessionToken;
            RefreshToken = refreshToken;

        }


    }
}
