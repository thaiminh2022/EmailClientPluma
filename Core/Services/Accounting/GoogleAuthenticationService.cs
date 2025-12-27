using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Models.Exceptions;
using EmailClientPluma.Core.Services.Storaging;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using System.Net;

namespace EmailClientPluma.Core.Services.Accounting;

/// <summary>
///     Authentication service implement for Google
///     It use Google Oauth2 to get user credentials + profile info
/// </summary>
internal class GoogleAuthenticationService(ILogger<GoogleAuthenticationService> logger) : IAuthenticationService
{
    private const string ClientSecret = @"secrets\secret.json";

    // Ask user permissions (gmail, profile)
    private static readonly string[] Scopes =
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

        if (!await InternetHelper.HasInternetConnection())
        {
            logger.LogError("No internet connection");
            throw new NoInternetException();
        }

        var tempId = Guid.NewGuid().ToString();

        // add a maximum timeout for authentication flow
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = cts.Token;

        try
        {
            logger.LogInformation("Initializing authentication for google");
            // prompt user to login
            var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromFile(ClientSecret).Secrets,
                Scopes,
                tempId,
                token,
                _dataStore);

            if (credentials is null)
            {
                logger.LogError("Cannot get credentials for google");
                return null;
            }

            var oauth2 = new Oauth2Service(new BaseClientService.Initializer
            {
                HttpClientInitializer = credentials
            });

            // userinfo timeout
            using var userInfoCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            userInfoCts.CancelAfter(TimeSpan.FromSeconds(15));

            var userInfo = await oauth2.Userinfo.Get().ExecuteAsync(userInfoCts.Token);
            if (userInfo == null)
            {
                logger.LogError("Cannot find user {mail} info via OAUTH2", credentials.UserId);
                return null;
            }

            await _dataStore.DeleteAsync<TokenResponse>(tempId);

            var newUserCred = new UserCredential(credentials.Flow, userInfo.Id, credentials.Token);
            await _dataStore.StoreAsync(userInfo.Id, newUserCred.Token);

            logger.LogInformation("Finish storing credentials info");
            logger.LogInformation("Finish AUTH FLOW");
            var cred = new Credentials(credentials.Token.AccessToken, credentials.Token.RefreshToken);
            return new AuthResponce(userInfo.Id, userInfo.Email, userInfo.Name, Provider.Google, cred);
        }
        catch (GoogleApiException ex)
        {
            logger.LogError(ex, "Cannot login because info is forbidden");
            if (ex.HttpStatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new AuthForbiddenException();
        }
        catch (TokenResponseException ex)
        {
            logger.LogError(ex, "Cannot login because info is forbidden");
            throw new AuthForbiddenException(inner: ex);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "User cancel auth flow");
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
        if (!await InternetHelper.HasInternetConnection())
        {
            logger.LogError("No internet connection");
            throw new NoInternetException();
        }

        logger.LogInformation("Validation init for account: {mail}", acc.Email);
        var tokenRes = await _dataStore.GetAsync<TokenResponse>(acc.ProviderUID);

        if (acc.ValidatedThisRun && !tokenRes.IsStale)
        {
            logger.LogInformation("Token is not staled");
            return true;
        }

        if (!acc.ValidatedThisRun || tokenRes.IsStale)
        {

            logger.LogWarning("{mail} token is staled or first validation", acc.Email);
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = GoogleClientSecrets.FromFile(ClientSecret).Secrets,
                Scopes = Scopes
            });
            var userCredentials = new UserCredential(flow, acc.ProviderUID, tokenRes);

            try
            {

                if (await userCredentials.RefreshTokenAsync(CancellationToken.None))
                {
                    acc.Credentials.SessionToken = userCredentials.Token.AccessToken;
                    acc.Credentials.RefreshToken = userCredentials.Token.RefreshToken;
                    await _dataStore.StoreAsync(acc.ProviderUID, userCredentials.Token);
                    logger.LogInformation("Refresh success");


                    acc.ValidatedThisRun = true;
                    return true;
                }
            }
            catch (TokenResponseException ex)
            {
                //throw new AuthRefreshException(inner: ex);
                // trying to do interactive

                logger.LogError(ex, "Cannot refresh token for account {email}, trying interactive", acc.Email);
            }
        }


        // can't silent login so doing interactive
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var token = cts.Token;

            var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromFile(ClientSecret).Secrets,
                Scopes,
                acc.ProviderUID,
                token,
                _dataStore
            );
            if (credentials is null)
            {
                logger.LogError("Cannot interactive logging for account {mail}, failing", acc.Email);
                return false;
            }

            await _dataStore.StoreAsync(acc.ProviderUID, credentials.Token);
            return true;
        }
        catch (TaskCanceledException ex)
        {
            //throw new AuthCancelException(inner: ex);
            // this is for logging
            logger.LogWarning(ex, "Authentication was cancel for account {email}", acc.Email);
        }

        return false;
    }

    public Provider GetProvider()
    {
        return Provider.Google;
    }
}