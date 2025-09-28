using EmailClientPluma.Core.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Data.Sqlite;
using System.Windows;

namespace EmailClientPluma.Core.Services
{
    interface IStorageService
    {
        Task<IEnumerable<Account>> GetAccountsAsync();
        Task StoreAccountAsync(Account account);
    }
    internal class StorageService : IStorageService
    {
        readonly string _connectionString;
        readonly SQLiteDataStore _tokenStore;


        public StorageService(string dbPath = @"C:\dev\CSharpProjects\EmailClientPluma\pluma.db")
        {
            _connectionString = $"Data Source={dbPath}";
            _tokenStore = new SQLiteDataStore(dbPath);
            Initialize();
        }

        public async Task<IEnumerable<Account>> GetAccountsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"SELECT * FROM ACCOUNTS";
            var reader = await command.ExecuteReaderAsync();

            List<Account> accounts = [];  
            while (reader.Read()) {

                var email = reader.GetString(0);
                var displayName = reader.GetString(1);
                var provider = (Provider)Enum.Parse(typeof(Provider), reader.GetString(2));

                // query token reponse
                try
                {
                    var token = await _tokenStore.GetAsync<TokenResponse>(email);
                    GoogleAuthorizationCodeFlow flow = new(new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecrets = GoogleClientSecrets.FromFile(GoogleAuthenticationService.CLIENT_SECRET).Secrets,
                        Scopes = GoogleAuthenticationService.scopes,
                        DataStore = _tokenStore,
                    });
                    var userCredentials = new UserCredential(flow, email, token);

                    var acc = new Account(displayName, provider, email, userCredentials);
                    accounts.Add(acc);
                }
                catch (Exception ex) { 
                    MessageBox.Show(ex.Message);    
                }

            }

            return accounts;
        }

        public async Task StoreAccountAsync(Account account)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO ACCOUNTS (EMAIL, DISPLAY_NAME, PROVIDER) 
                                    VALUES ($email, $display_name, $provider)
                                   ";

            command.Parameters.AddWithValue("$email", account.Email);
            command.Parameters.AddWithValue("$display_name", account.DisplayName);
            command.Parameters.AddWithValue("$provider", account.Provider.ToString());


            await command.ExecuteNonQueryAsync();
        }

        private void Initialize()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS ACCOUNTS (
                                    EMAIL TEXT PRIMARY KEY,
                                    DISPLAY_NAME TEXT NOT NULL,
                                    PROVIDER TEXT
                                    );
                                  ";
            command.ExecuteNonQuery();
        }
    }
}
