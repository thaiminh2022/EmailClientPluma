using EmailClientPluma.Core.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Interop;
using EmailClientPluma.Core.Models.Exceptions;

namespace EmailClientPluma.Core.Services.Accounting;

internal interface IMicrosoftClientApp
{
    string ClientId { get; }
    string Tenant { get; }
    string[] Scopes { get; }

    IPublicClientApplication PublicClient { get; }
    Task SignOutAsync(Account acc);
}

internal class MicrosoftAuthenticationService : IAuthenticationService, IMicrosoftClientApp
{
    public string ClientId { get; } = "19ea33aa-6d46-4fb9-b094-d53801280a34";
    public string Tenant { get; } = "common";

    public string[] Scopes { get; } =
    [
        "User.Read", // Required for /me endpoint
        "Mail.Read", // For reading emails
        "Mail.ReadWrite", // For modifying emails (flags, move, delete)
        "Mail.Send", // For sending emails
    ];

    private IPublicClientApplication? _publicClient;

    public IPublicClientApplication PublicClient
    {
        get
        {
            _publicClient ??= InitializePublicClient();
            return _publicClient;
        }
    }


    public async Task SignOutAsync(Account acc)
    {
        var accounts = await PublicClient.GetAccountsAsync();
        var microsoftAccount = accounts.FirstOrDefault(x => x.HomeAccountId.Identifier == acc.ProviderUID);

        if (microsoftAccount is not null)
        {
            await PublicClient.RemoveAsync(microsoftAccount);
        }
        else
        {
            MessageBoxHelper.Info("Account already sign out, delete info only");
        }
    }


    private IntPtr GetWindow()
    {
        return new WindowInteropHelper(Application.Current.MainWindow!).Handle;
    }

    private IPublicClientApplication InitializePublicClient()
    {
        var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);

        var client = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithRedirectUri("http://localhost")
            .WithAuthority($"https://login.microsoftonline.com/{Tenant}")
            .WithParentActivityOrWindow(GetWindow)
            .WithBroker(brokerOptions)
            .Build();

        var cacheHelper = CreateCacheHelperAsync().GetAwaiter().GetResult();
        cacheHelper.RegisterCache(client.UserTokenCache);

        return client;
    }

    private static async Task<MsalCacheHelper> CreateCacheHelperAsync()
    {
        // Since this is a WPF application, only Windows storage is configured
        var storageProperties = new StorageCreationPropertiesBuilder(
                Helper.MsalCachePath,
                MsalCacheHelper.UserRootDirectory)
            .Build();

        var cacheHelper = await MsalCacheHelper.CreateAsync(
            storageProperties,
            new TraceSource("MSAL.CacheTrace")).ConfigureAwait(false);
        return cacheHelper;
    }

    public Provider GetProvider()
    {
        return Provider.Microsoft;
    }

    public async Task<AuthResponce?> AuthenticateAsync()
    {
        try
        {
            var result = await PublicClient.AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();

            var key = result.Account.HomeAccountId.Identifier;
            var userInfo = await AcquireUserInfo(result.AccessToken);

            if (userInfo is null) return null;

            return new AuthResponce(key,
                userInfo.Value.Mail ?? result.Account.Username,
                userInfo.Value.DisplayName,
                Provider.Microsoft,
                new Credentials(string.Empty, string.Empty));
        }
        catch (MsalClientException ex)
        {
            throw new AuthFailedException(msg: "Lỗi phần mềm, xin đừng sử dụng chức năng này", inner: ex);
        }
        catch (MsalServiceException ex)
        {
            throw new AuthFailedException(inner: ex);
        }
    }
    private struct UserProfile
    {
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("mail")]
        public string? Mail { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    private readonly HttpClient _httpClient = new HttpClient();
    private async Task<UserProfile?> AcquireUserInfo(string token)
    {
        const string url = "https://graph.microsoft.com/v1.0/me";
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            //Add the token in Authorization header
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            var me = JsonConvert.DeserializeObject<UserProfile>(content);
            return me;
        }
        catch (Exception ex)
        {
            MessageBoxHelper.Error("Getting user info error: ", ex);
            return null;
        }
    }

    public async Task<bool> ValidateAsync(Account acc)
    {
        AuthenticationResult? res = null;
        IAccount? microsoftAccount = null;

        try
        {
            var accounts = await PublicClient.GetAccountsAsync();
            microsoftAccount = accounts.FirstOrDefault(x => x.HomeAccountId.Identifier == acc.ProviderUID);

            if (microsoftAccount is null)
            {
                // do interactive login
            }
            else
            {
                res = await PublicClient
                    .AcquireTokenSilent(Scopes, microsoftAccount)
                    .ExecuteAsync();

                if (res is not null)
                {
                    return true;
                }
            }
        }
        catch (MsalUiRequiredException)
        {
            // interactive login
        }

        try
        {
            if (microsoftAccount is null)
            {
                res = await PublicClient.AcquireTokenInteractive(Scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync();
            }
            else
            {
                res = await PublicClient.AcquireTokenInteractive(Scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .WithAccount(microsoftAccount)
                    .ExecuteAsync();
            }
        }
        catch (Exception ex)
        {
            //throw new AuthFailedException(inner: ex);
            //this is for logging
        }
        return res is not null;
    }
}