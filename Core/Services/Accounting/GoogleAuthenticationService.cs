using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Storaging;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;
using System.Net;
using EmailClientPluma.Core.Models.Exceptions;

namespace EmailClientPluma.Core.Services.Accounting;

/// <summary>
///     Authentication service implement for google
///     It use google Oauth2 to get user credentials + profile info
/// </summary>
internal class GoogleAuthenticationService : IAuthenticationService
{
    public const string CLIENT_SECRET = @"secrets\secret.json";

    // Ask user permissions (gmail, profile)
    public static readonly string[] scopes =
    [
        "https://mail.google.com/",
        Oauth2Service.Scope.UserinfoEmail,
        Oauth2Service.Scope.UserinfoProfile
    ];

    private readonly GoogleDataStore _dataStore = new(Helper.DatabasePath);


    /// <summary>
    ///     Try to ask for authenticating a new account
    /// </summary>
    /// <returns>The account</returns>
    public async Task<AuthResponce?> AuthenticateAsync()
    {
        var tempId = Guid.NewGuid().ToString();
        try
        {
            // prompt user to login
            var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromFile(CLIENT_SECRET).Secrets,
                scopes,
                tempId,
                CancellationToken.None,
                _dataStore);

            if (credentials is null)
            {
                throw new AuthFailedException();
            }

            var oauth2 = new Oauth2Service(new BaseClientService.Initializer
            {
                HttpClientInitializer = credentials
            });

            var userInfo = await oauth2.Userinfo.Get().ExecuteAsync();
            if (userInfo == null)
            {
                MessageBoxHelper.Error("Cannot find user info");
                return null;
            }

            await _dataStore.DeleteAsync<TokenResponse>(tempId);

            var newUserCred = new UserCredential(credentials.Flow, userInfo.Id, credentials.Token);
            await _dataStore.StoreAsync(userInfo.Id, newUserCred.Token);


            var cred = new Credentials(credentials.Token.AccessToken, credentials.Token.RefreshToken);
            return new AuthResponce(userInfo.Id, userInfo.Email, userInfo.Name, Provider.Google, cred);
        }
        catch (GoogleApiException ex)
        {
            if (ex.HttpStatusCode == HttpStatusCode.Unauthorized ||
                ex.HttpStatusCode == HttpStatusCode.Forbidden)
                throw new AuthForbiddenException();
        }
        catch (TokenResponseException ex)
        {
            throw new AuthForbiddenException(inner: ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new AuthCancelException(inner: ex);
        }


        return null;
    }

    /// <summary>
    ///     Try to validate by relogging
    /// </summary>
    /// <param name="acc">The account</param>
    /// <returns>true if is valid or failed</returns>
    public async Task<bool> ValidateAsync(Account acc)
    {
        // reconstruct user credentials to check
        var tokenRes = await _dataStore.GetAsync<TokenResponse>(acc.ProviderUID);

        if (tokenRes.IsStale)
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = GoogleClientSecrets.FromFile(CLIENT_SECRET).Secrets,
                Scopes = scopes
            });
            var userCredentials = new UserCredential(flow, acc.ProviderUID, tokenRes);

            try
            {
                if (await userCredentials.RefreshTokenAsync(CancellationToken.None))
                {
                    acc.Credentials.SessionToken = userCredentials.Token.AccessToken;
                    acc.Credentials.RefreshToken = userCredentials.Token.RefreshToken;
                    await _dataStore.StoreAsync(acc.ProviderUID, userCredentials.Token);

                    return true;
                }
            }
            catch (TokenResponseException ex)
            {
                //throw new AuthRefreshException(inner: ex);
                // trying to do interactive
            }
        }
        else
        {
            return true;
        }

        // can't silent login so doing interactive
        try
        {
            var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromFile(CLIENT_SECRET).Secrets,
                scopes,
                acc.ProviderUID,
                CancellationToken.None,
                _dataStore
            );
            if (credentials is null)
            {
                throw new AuthFailedException();
            }

            await _dataStore.StoreAsync(acc.ProviderUID, credentials.Token);
            return true;
        }
        catch (TaskCanceledException ex)
        {
            //throw new AuthCancelException(inner: ex);
            // this is for logging
        }

        return false;
    }

    public Provider GetProvider()
    {
        return Provider.Google;
    }
}