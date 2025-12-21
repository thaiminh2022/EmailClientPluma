using Dapper;
using EmailClientPluma.Core.Models;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Data.Sqlite;

namespace EmailClientPluma.Core.Services.Storaging;

internal class AccountStorage
{
    private readonly string _connectionString;

    private readonly GoogleDataStore _tokenStore;

    public AccountStorage(GoogleDataStore tokenStore, string connectionString)
    {
        _tokenStore = tokenStore;
        _connectionString = connectionString;
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public async Task<IEnumerable<Account>> GetAccountsAsync()
    {
        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<AccountRow>(
            """
            SELECT PROVIDER_UID, PROVIDER, EMAIL, DISPLAY_NAME, PAGINATION_TOKEN, LAST_SYNC_TOKEN FROM ACCOUNTS
            """
        );
        List<Account> accounts = [];
        foreach (var row in rows)
        {
            try
            {
                Credentials cred;

                if (!Enum.TryParse<Provider>(row.PROVIDER, out var provider))
                {
                    continue;
                }

                if (provider == Provider.Google)
                {
                    var token = await _tokenStore.GetAsync<TokenResponse>(row.PROVIDER_UID);
                    cred = new Credentials(token.AccessToken, token.RefreshToken);
                }
                else
                {
                    cred = new Credentials(string.Empty, string.Empty);
                }

                var acc = new Account(row.PROVIDER_UID, row.EMAIL, row.DISPLAY_NAME, provider, cred)
                {
                    PaginationToken = row.PAGINATION_TOKEN,
                    LastSyncToken = row.LAST_SYNC_TOKEN
                };
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
        await using var connection = CreateConnection();

        var sql = """
                  INSERT INTO ACCOUNTS
                      (PROVIDER_UID, PROVIDER, EMAIL, DISPLAY_NAME, PAGINATION_TOKEN, LAST_SYNC_TOKEN)
                  VALUES
                      (@ProviderUID, @Provider, @Email, @DisplayName, @PaginationToken, @LastSyncToken)
                  ON CONFLICT (PROVIDER_UID)
                  DO UPDATE SET
                      PROVIDER         = excluded.PROVIDER,
                      EMAIL            = excluded.EMAIL,
                      DISPLAY_NAME     = excluded.DISPLAY_NAME,
                      PAGINATION_TOKEN = excluded.PAGINATION_TOKEN,
                      LAST_SYNC_TOKEN  = excluded.LAST_SYNC_TOKEN;
                  """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            account.ProviderUID,
            Provider = account.Provider.ToString(),
            account.Email,
            account.DisplayName,
            account.PaginationToken,
            account.LastSyncToken
        });

        return affected;
    }

    public async Task UpdatePaginationAndNextTokenAsync(Account account)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        var tx = connection.BeginTransaction();

        var sql = """
                    UPDATE ACCOUNTS SET PAGINATION_TOKEN = @PaginationToken, LAST_SYNC_TOKEN = @LastSyncToken
                    WHERE  PROVIDER_UID = @ProviderUID
                  """;

        await connection.ExecuteAsync(sql, new
        {
            account.PaginationToken,
            account.LastSyncToken,
            account.ProviderUID
        }, tx);

        await tx.CommitAsync();
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
            case Provider.Microsoft:
                break;
            default:
                throw new NotImplementedException("Deleting account for this provider isnt implemented yet");
        }
    }
}