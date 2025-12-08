using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Storaging;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;

namespace EmailClientPluma.Core.Services.Accounting
{

    /// <summary>
    /// Authentication service implement for google
    /// It use google Oauth2 to get user credentials + profile info
    /// </summary>
    internal class GoogleAuthenticationService : IAuthenticationService
    {
        readonly SQLiteDataStore _dataStore = new(Helper.DatabasePath);

        // Ask user permissions (gmail, profile)
        public static readonly string[] scopes = [
            @"https://mail.google.com/",
            Oauth2Service.Scope.UserinfoEmail,
            Oauth2Service.Scope.UserinfoProfile,
        ];
        public const string CLIENT_SECRET = @"secrets\secret.json";


        /// <summary>
        /// Try to ask for authentiocating a new account
        /// </summary>
        /// <returns>The account</returns>
        public async Task<AuthResponce?> AuthenticateAsync()
        {
            string tempID = Guid.NewGuid().ToString();
            try
            {
                // prompt user to login
                UserCredential credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromFile(CLIENT_SECRET).Secrets,
                    scopes,
                    tempID,
                    CancellationToken.None,
                    _dataStore);


                var oauth2 = new Oauth2Service(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credentials,
                });

                var userInfo = await oauth2.Userinfo.Get().ExecuteAsync();
                if (userInfo == null)
                {
                    MessageBoxHelper.Error("Cannot find user info");
                    return null;
                }

                await _dataStore.DeleteAsync<TokenResponse>(tempID);

                var newUserCred = new UserCredential(credentials.Flow, userInfo.Id, credentials.Token);
                await _dataStore.StoreAsync(userInfo.Id, newUserCred.Token);


                var cred = new Credentials(credentials.Token.AccessToken, credentials.Token.RefreshToken);
                return new AuthResponce(userInfo.Id, userInfo.Email, userInfo.Name, Provider.Google, cred);
            }
            catch (GoogleApiException ex)
            {
                if (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    MessageBoxHelper.Error("Nguoi dung khong duoc phep dang nhap");
                }
            }
            catch (TokenResponseException ex)
            {
                MessageBoxHelper.Info($"Nguoi dung huy dang nhap: {ex}");
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Error(ex);
            }
            return null;
        }
        /// <summary>
        /// Try validate by relogging
        /// </summary>
        /// <param name="acc">The account</param>
        /// <returns>true if is valid or failed</returns>
        public async Task<bool> ValidateAsync(Account acc)
        {
            // reconstruct user credentials to check
            var tokenRes = await _dataStore.GetAsync<TokenResponse>(acc.ProviderUID);

            if (tokenRes.IsStale)
            {
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer()
                {
                    ClientSecrets = GoogleClientSecrets.FromFile(CLIENT_SECRET).Secrets,
                    Scopes = scopes,
                });
                var usercred = new UserCredential(flow, acc.ProviderUID, tokenRes);

                try
                {
                    if (await usercred.RefreshTokenAsync(default))
                    {
                        acc.Credentials.SessionToken = usercred.Token.AccessToken;
                        acc.Credentials.RefreshToken = usercred.Token.RefreshToken;
                        await _dataStore.StoreAsync(acc.ProviderUID, usercred.Token);

                        return true;
                    }
                }
                catch (TokenResponseException)
                {
                    MessageBoxHelper.Error($"Hay dang nhap lai tai khoan: {acc.Email}");
                }
            }
            else
            {
                return true;
            }

            try
            {
                UserCredential credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                     GoogleClientSecrets.FromFile(CLIENT_SECRET).Secrets,
                     scopes,
                     acc.ProviderUID,
                     CancellationToken.None,
                     _dataStore
                );
                await _dataStore.StoreAsync(acc.ProviderUID, credentials.Token);

                return true;
            }
            catch (TaskCanceledException ex)
            {
                MessageBoxHelper.Error(ex.Message);
            }
            return false;
        }

        public Provider GetProvider()
        {
            return Provider.Google;
        }
    }
}
