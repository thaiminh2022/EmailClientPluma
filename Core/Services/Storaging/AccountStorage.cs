using Dapper;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Data.Sqlite;

namespace EmailClientPluma.Core.Services.Storaging
{
    internal class AccountStorage
    {
        private readonly string _connectionString;
        private SqliteConnection CreateConnection() => new(_connectionString);
        private readonly SQLiteDataStore _tokenStore;

        public AccountStorage(SQLiteDataStore tokenStore, string connectionString)
        {
            _tokenStore = tokenStore;
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<Account>> GetAccountsAsync()
        {
            await using var connection = CreateConnection();
            var rows = await connection.QueryAsync<AccountRow>(
                @"SELECT PROVIDER_UID, PROVIDER, EMAIL, DISPLAY_NAME FROM ACCOUNTS"
            );
            List<Account> accounts = [];
            foreach (var row in rows)
            {
                try
                {
                    var token = await _tokenStore.GetAsync<TokenResponse>(row.PROVIDER_UID);
                    var cred = new Credentials(token.AccessToken, token.RefreshToken);
                    var provider = Enum.Parse<Provider>(row.PROVIDER);

                    var acc = new Account(row.PROVIDER_UID, row.EMAIL, row.DISPLAY_NAME, provider, cred);
                    accounts.Add(acc);
                }
                catch (Exception ex)
                {
                    MessageBoxHelper.Error(ex.Message);
                }
            }

            return accounts;
        }
        public async Task<int> StoreAccountAsync(Account account)
        {
            using var connection = CreateConnection();

            string sql = @" INSERT INTO ACCOUNTS (PROVIDER_UID, PROVIDER, EMAIL, DISPLAY_NAME) 
                            VALUES (@ProviderUID, @Provider, @Email, @DisplayName);
                          ";

            var affected = await connection.ExecuteAsync(sql, new
            {
                account.ProviderUID,
                Provider = account.Provider.ToString(),
                account.Email,
                account.DisplayName
            });

            return affected;
        }

        public async Task RemoveAccountAsync(Account account)
        {
            await using var connection = CreateConnection();

            const string sql = @"DELETE FROM ACCOUNTS WHERE PROVIDER_UID = @ProviderUID;";
            await connection.ExecuteAsync(sql, new { account.ProviderUID });

            switch (account.Provider)
            {
                case Provider.Google:
                    await _tokenStore.DeleteAsync<TokenResponse>(account.ProviderUID);
                    break;
                default:
                    throw new NotImplementedException("Deleting account for this provider isnt implemented yet");
            }
        }
    }
}
