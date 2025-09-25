using EmailClientPluma.Core.Models;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Oauth2.v2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Windows;

namespace EmailClientPluma.Core.Services
{

    /// <summary>
    /// Authentication service implement for google
    /// It use google Oauth2 to get user credentials + profile info
    /// </summary>
    internal class GoogleAuthenticationService : IAuthenticationService
    {
        // Ask user permissions (gmail, profile)
        readonly string[] scopes = [
            @"https://mail.google.com/",
            Oauth2Service.Scope.UserinfoEmail,
            Oauth2Service.Scope.UserinfoProfile,
         ];
        const string SECRET_PATH = @"secrets\secret.json";
        const string CRED_PATH = @"EmailClientPluma\tokens";


        /// <summary>
        /// Try to ask for authentiocating a new account
        /// </summary>
        /// <returns>The account</returns>
        public async Task<Account?> AuthenticateAsync()
        {
            string tempID = Guid.NewGuid().ToString();
            try
            {
                // Credentials will autosave to %AppData%\EmailClientPluma\tokens
                // prompt user to login
                var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromFile(SECRET_PATH).Secrets,
                    scopes,
                    tempID,
                    CancellationToken.None,
                    new FileDataStore(CRED_PATH, false));


                var oauth2 = new Oauth2Service(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credentials,
                });

                var userInfo = await oauth2.Userinfo.Get().ExecuteAsync();
                if (userInfo == null)
                {
                    MessageBox.Show("Cannot find user info");
                    return null;
                }

                return new Account(userInfo.Name, Provider.Google,  userInfo.Email, credentials);
            }
            catch (GoogleApiException ex)
            {
                if (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    MessageBox.Show("Nguoi dung khong duoc phep dang nhap");
                }
            }
            catch (TokenResponseException ex)
            {
                MessageBox.Show($"Nguoi dung huy dang nhap: {ex.Error}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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
            try
            {
                var credentials = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromFile(SECRET_PATH).Secrets,
                    scopes,
                    acc.Credentials.UserId,
                    CancellationToken.None,
                    new FileDataStore(CRED_PATH, false));

                if (credentials != null)
                {
                    return true;
                }
            }
            catch (TaskCanceledException ex)
            {
                MessageBox.Show(ex.Message);
            }
            return false;
        }

        public Provider GetProvider()
        {
            return Provider.Google;
        }
    }
}
