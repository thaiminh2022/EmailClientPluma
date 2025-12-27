using System.Collections.ObjectModel;

namespace EmailClientPluma.Core.Models;

/// <summary>
///     Store account infos lol
/// </summary>
internal class Account
{
    // NOT STORED
    public bool FirstTimeHeaderFetched = false;
    public bool NoMoreOlderEmail = false;
    public bool ValidatedThisRun = false;


    public Account(string providerUid, string email, string displayName, Provider provider, Credentials credentials)
    {
        ProviderUID = providerUid;
        Email = email;
        DisplayName = displayName;
        Provider = provider;
        Credentials = credentials;
        OwnedLabels = new ObservableCollection<EmailLabel>(EmailLabel.Labels);
    }

    public Account(AuthResponce authResponse)
    {
        ProviderUID = authResponse.ProviderUID;
        Email = authResponse.Email;
        DisplayName = authResponse.DisplayName;
        Provider = authResponse.Provider;
        Credentials = authResponse.Credentials;
        OwnedLabels = new ObservableCollection<EmailLabel>(EmailLabel.Labels);
    }

    // DATABASE STORED
    public string ProviderUID { get; set; }
    public string Email { get; set; }
    public string DisplayName { get; set; }
    public Provider Provider { get; set; }
    public ObservableCollection<Email> Emails { get; set; } = [];

    public string? PaginationToken { get; set; }
    public string? LastSyncToken { get; set; }
    public Credentials Credentials { get; set; }

    public ObservableCollection<EmailLabel> OwnedLabels { get; set; }
}