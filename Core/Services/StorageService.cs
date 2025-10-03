using EmailClientPluma.Core.Models;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Data.Sqlite;
using System.Windows;

namespace EmailClientPluma.Core.Services
{
    interface IStorageService
    {
        Task<IEnumerable<Account>> GetAccountsAsync();
        Task<int> StoreAccountAsync(Account account);
        Task RemoveAccountAsync(Account account);

        Task<IEnumerable<Email>> GetEmailsAsync(Account acc);
        Task StoreEmailAsync(Account acc);

    }
    internal class StorageService : IStorageService
    {
        readonly string _connectionString;
        readonly SQLiteDataStore _tokenStore;


        public StorageService()
        {
            _connectionString = $"Data Source={AppPaths.DatabasePath}";
            _tokenStore = new SQLiteDataStore(AppPaths.DatabasePath);
            Initialize();
        }

        public async Task<IEnumerable<Account>> GetAccountsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"SELECT * FROM ACCOUNTS";
            using var reader = await command.ExecuteReaderAsync();

            List<Account> accounts = [];
            while (reader.Read())
            {

                var providerUID = reader.GetString(0);
                var provider = (Provider)Enum.Parse(typeof(Provider), reader.GetString(1));
                var email = reader.GetString(2);
                var displayName = reader.GetString(3);

                // query token reponse
                try
                {
                    var token = await _tokenStore.GetAsync<TokenResponse>(providerUID);
                    var cred = new Credentials(token.AccessToken, token.RefreshToken);
                    var acc = new Account(providerUID, email, displayName, provider, cred);
                    accounts.Add(acc);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }

            return accounts;
        }
        public async Task<int> StoreAccountAsync(Account account)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO ACCOUNTS (PROVIDER_UID, PROVIDER, EMAIL, DISPLAY_NAME) 
                                    VALUES ($provider_uid, $provider, $email, $display_name)
                                   ";
            command.Parameters.AddWithValue("$provider_uid", account.ProviderUID);
            command.Parameters.AddWithValue("$email", account.Email);
            command.Parameters.AddWithValue("$display_name", account.DisplayName);
            command.Parameters.AddWithValue("$provider", account.Provider.ToString());

            var row = await command.ExecuteNonQueryAsync();

            return row;
        }


        public async Task RemoveAccountAsync(Account account)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            // delete account and its emails since foreign keys
            command.CommandText = @"DELETE FROM ACCOUNTS WHERE PROVIDER_UID = $provider_uid";
            command.Parameters.AddWithValue("$provider_uid", account.ProviderUID);

            // delete googleStoreData
            switch (account.Provider)
            {
                case Provider.Google:
                    await _tokenStore.DeleteAsync<TokenResponse>(account.ProviderUID);
                    break;
                default:
                    throw new NotImplementedException("Deleting account for this provider isnt implemented yet");
            }

            await command.ExecuteNonQueryAsync();
        }


        private void Initialize()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS ACCOUNTS (
                                    PROVIDER_UID     TEXT PRIMARY KEY NOT NULL,             
                                    PROVIDER         TEXT NOT NULL,            
                                    EMAIL            TEXT NOT NULL,
                                    DISPLAY_NAME     TEXT,
                                    UNIQUE (PROVIDER, PROVIDER_UID, EMAIL)         
                                    );
                                  ";
            command.ExecuteNonQuery();

            command.CommandText = @"CREATE TABLE IF NOT EXISTS EMAILS (
                                    EMAIL_ID    INTEGER  PRIMARY KEY AUTOINCREMENT,
	                                OWNER_ID	TEXT,
	                                SUBJECT	    TEXT,
	                                BODY	TEXT,
	                                ""FROM""	TEXT,
	                                ""TO""	    TEXT,
	                                FOREIGN KEY(OWNER_ID) REFERENCES ACCOUNTS(PROVIDER_UID) ON DELETE CASCADE
                                );";

            command.ExecuteNonQuery();
        }


        public async Task StoreEmailAsync(Account acc)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            command.CommandText = @"INSERT INTO EMAILS (OWNER_ID, SUBJECT, BODY,""FROM"", ""TO"") 
                                    VALUES ($owner_id, $subject, $body,$from, $to)";
            foreach (var item in acc.Emails)
            {
                command.Parameters.AddWithValue("$owner_id", acc.ProviderUID);
                command.Parameters.AddWithValue("$subject", item.Subject);
                command.Parameters.AddWithValue("$body", item.Body);
                command.Parameters.AddWithValue("$from", item.From);
                command.Parameters.AddWithValue("$to", string.Join(',', item.To));

                await command.ExecuteNonQueryAsync();
                command.Parameters.Clear();
            }
        }
        public async Task<IEnumerable<Email>> GetEmailsAsync(Account acc)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            command.CommandText = @"SELECT * FROM EMAILS WHERE OWNER_ID = $ownerid";
            command.Parameters.AddWithValue("$ownerid", acc.ProviderUID);

            using var reader = await command.ExecuteReaderAsync();

            List<Email> emails = [];

            while (reader.Read())
            {
                var emailID = reader.GetInt32(0);
                var emailAccountOwnerID = reader.GetString(1);
                var subject = reader.GetString(2);
                var body = reader.GetString(3);
                var from = reader.GetString(4);
                var to = reader.GetString(5);

                var email = new Email(emailAccountOwnerID, subject, body, from, to, [])
                {
                    EmailID = emailID,
                };
                emails.Add(email);
            }
            return emails;
        }

    }
}
