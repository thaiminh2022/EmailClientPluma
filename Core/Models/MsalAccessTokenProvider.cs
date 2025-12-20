
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

namespace EmailClientPluma.Core.Models
{
    internal class MsalAccessTokenProvider : IAccessTokenProvider
    {
        private IPublicClientApplication _publicClient;
        private string[] _scopes;

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();

        private readonly string? _loginHint;
        public MsalAccessTokenProvider(IPublicClientApplication publicClient, string[] scopes, string? loginHint)
        {
            _publicClient = publicClient;
            _scopes = scopes;
            _loginHint = loginHint;
        }


        public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var accs = await _publicClient.GetAccountsAsync();
            var accounts = accs.ToList();

            var account = accounts.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(_loginHint) &&
                string.Equals(a.HomeAccountId.Identifier, _loginHint, StringComparison.OrdinalIgnoreCase)) ?? accounts.FirstOrDefault();

            try
            {
                if (account is not null)
                {
                    var silent = await _publicClient
                        .AcquireTokenSilent(_scopes, account)
                        .ExecuteAsync(cancellationToken);

                    return silent.AccessToken;
                }
            }
            catch (MsalUiRequiredException)
            {
                // Expected when no cached token / consent / CA policy etc.
            }

            // Fallback to interactive only when required
            var interactive = await _publicClient.AcquireTokenInteractive(_scopes)
                .WithPrompt(Prompt.SelectAccount) // helps when multiple accounts exist
                .ExecuteAsync(cancellationToken);

            return interactive.AccessToken;

        }
    }
}
