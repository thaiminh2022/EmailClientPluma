using EmailClientPluma.Core.Services.Accounting;
using System.Collections.ObjectModel;

namespace EmailClientPluma.Core.Models
{
    /// <summary>
    /// Store account infos lol
    /// </summary>
    internal class Account
    {
        // DATABASE STORED
        public string ProviderUID { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public Provider Provider { get; set; }
        public ObservableCollection<Email> Emails { get; set; } = [];

        public Credentials Credentials { get; set; }


        // NOT STORED
        public bool IsHeadersFetched => Emails.Count > 0;
        public int UnreadCount { get; set; }

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
}
