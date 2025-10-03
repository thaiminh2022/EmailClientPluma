using EmailClientPluma.Core.Services;

namespace EmailClientPluma.Core.Models
{
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
