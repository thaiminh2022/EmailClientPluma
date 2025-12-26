using Dapper;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Models.Exceptions;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace EmailClientPluma.Core.Services.Storaging;

internal class AccountStorage(GoogleDataStore tokenStore, string connectionString, ILogger<StorageService> logger)
{
    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(connectionString);
    }

    public async Task<IEnumerable<Account>> GetAccountsAsync()
    {
        logger.LogInformation("Getting accounts information");
        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<AccountRow>(
            "SELECT PROVIDER_UID, PROVIDER, EMAIL, DISPLAY_NAME, PAGINATION_TOKEN, LAST_SYNC_TOKEN FROM ACCOUNTS"
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
                    var token = await tokenStore.GetAsync<TokenResponse>(row.PROVIDER_UID);
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
                logger.LogInformation("Account {email} read success", acc.Email);
                accounts.Add(acc);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "READING ACCOUNT FAILED, THIS IS DUE TO PROGRAM ERROR");
                throw new ReadAccountException(inner: ex);
            }
        }


        return accounts;
    }

    public async Task<int> StoreAccountAsync(Account account)
    {
        await using var connection = CreateConnection();

        logger.LogInformation("Storing info for {email}", account.Email);

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
        try
        {
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
        catch (Exception ex)
        {
            logger.LogCritical("CANNOT STORE ACCOUNT, THIS IS A PROGRAM ERROR");
            throw new WriteAccountException(inner: ex);
        }
    }

    public async Task UpdatePaginationAndNextTokenAsync(Account account)
    {
        logger.LogInformation("Storing token info for {email}", account.Email);
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        var tx = connection.BeginTransaction();

        var sql = """
                    UPDATE ACCOUNTS SET PAGINATION_TOKEN = @PaginationToken, LAST_SYNC_TOKEN = @LastSyncToken
                    WHERE  PROVIDER_UID = @ProviderUID
                  """;
        try
        {
            await connection.ExecuteAsync(sql, new
            {
                account.PaginationToken,
                account.LastSyncToken,
                account.ProviderUID
            }, tx);
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CANNOT STORE TOKEN INFOS FOR {email}, THIS IS A PROGRAM ERROR", account.Email);
            throw new WriteAccountException();
        }
    }

    public async Task RemoveAccountAsync(Account account)
    {
        logger.LogInformation("Removing account {}", account.Email);
        await using var connection = CreateConnection();

        const string sql = @"DELETE FROM ACCOUNTS WHERE PROVIDER_UID = @ProviderUID;";

        try
        {
            await connection.ExecuteAsync(sql, new { account.ProviderUID });

            switch (account.Provider)
            {
                case Provider.Google:
                    await tokenStore.DeleteAsync<TokenResponse>(account.ProviderUID);
                    break;
                case Provider.Microsoft:
                    break;
                default:
                    throw new NotImplementedException("Deleting account for this provider isn't implemented yet");
            }
        }
        catch (NotImplementedException)
        {
            logger.LogCritical("THE USER PROVIDER IS NOT IMPLEMENTED, THIS IS A PROGRAM ERROR");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CANNOT DELETE USER, THIS IS A PROGRAM ERROR");
            throw new WriteAccountException(inner: ex);
        }
    }
}