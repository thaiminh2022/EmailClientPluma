using Dapper;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Storaging;
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
        Task StoreEmailAsync(Account acc, Email mail);
        Task UpdateEmailBodyAsync(Email email);

    }
    internal partial class StorageService : IStorageService
    {
        readonly string _connectionString;
        readonly SQLiteDataStore _tokenStore;
        readonly StorageMigratior _migrator;

        private SqliteConnection CreateConnection() => new SqliteConnection(_connectionString);

        public StorageService()
        {
            _connectionString = $"Data Source={Helper.DatabasePath}";
            _migrator = new StorageMigratior(_connectionString);
            _tokenStore = new SQLiteDataStore(Helper.DatabasePath);

            _migrator.Migrate();
        }


        #region Accounts

        public async Task<IEnumerable<Account>> GetAccountsAsync()
        {
            using var connection = CreateConnection();
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
            using var connection = CreateConnection();

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

        #endregion
        #region emails
        public async Task StoreEmailsInternal(Account acc, IEnumerable<Email> mails)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            var sql = @"
                                INSERT INTO EMAILS (
                                    IMAP_UID,
                                    IMAP_UID_VALIDITY,
                                    FOLDER_FULLNAME,
                                    MESSAGE_ID,
                                    OWNER_ID,
                                    IN_REPLY_TO,
                                    SUBJECT,
                                    BODY,
                                    FROM_ADDRESS,
                                    TO_ADDRESS,
                                    DATE
                                ) VALUES (
                                    @ImapUid,
                                    @ImapUidValidity,
                                    @FolderFullName,
                                    @MessageId,
                                    @OwnerId,
                                    @InReplyTo,
                                    @Subject,
                                    @Body,
                                    @From,
                                    @To,
                                    @Date
                                )
                                ON CONFLICT (OWNER_ID, FOLDER_FULLNAME, IMAP_UID_VALIDITY, IMAP_UID)
                                DO UPDATE SET
                                    SUBJECT            = excluded.SUBJECT,
                                    BODY               = COALESCE(excluded.BODY, EMAILS.BODY),
                                    FROM_ADDRESS       = excluded.FROM_ADDRESS,
                                    TO_ADDRESS         = excluded.TO_ADDRESS,
                                    DATE               = excluded.DATE;
                                ";
            var parameters = mails.Select(m =>
            {
                var msgPart = m.MessageParts;
                var msgId = m.MessageIdentifiers;

                return new
                {
                    ImapUid = msgId.ImapUID,
                    ImapUidValidity = msgId.ImapUIDValidity,
                    FolderFullName = msgId.FolderFullName,
                    MessageId = msgId.MessageID,
                    OwnerId = acc.ProviderUID,
                    InReplyTo = msgId.InReplyTo,

                    Subject = msgPart.Subject,
                    Body = msgPart.Body,
                    From = msgPart.From,
                    To = msgPart.To,
                    Date = msgPart.Date?.ToString("o")
                };
            });

            try
            {
                await connection.ExecuteAsync(sql, parameters);
            }
            catch (Exception ex)
            {
             MessageBoxHelper.Error(ex.Message);
            }
        }


        public async Task StoreEmailAsync(Account acc) => await StoreEmailsInternal(acc, acc.Emails);

        public async Task StoreEmailAsync(Account acc, Email mail) => await StoreEmailsInternal(acc, [mail]);

        public async Task<IEnumerable<Email>> GetEmailsAsync(Account acc)
        {
            using var connection = CreateConnection();
            var sql = @"
                        SELECT  EMAIL_ID,
                                IMAP_UID,
                                IMAP_UID_VALIDITY,
                                FOLDER_FULLNAME,
                                MESSAGE_ID,
                                OWNER_ID,
                                IN_REPLY_TO,
                                SUBJECT,
                                BODY,
                                FROM_ADDRESS,
                                TO_ADDRESS,
                                DATE
                        FROM EMAILS
                        WHERE OWNER_ID = @OwnerId
                        --ORDER BY DATE DESC
                       ";
            var rows = await connection.QueryAsync<EmailRow>(sql, new { OwnerId = acc.ProviderUID });
            var emails = rows.Select(r =>
            {
                DateTimeOffset? date = null;
                if (!string.IsNullOrEmpty(r.DATE))
                {
                    date = DateTimeOffset.Parse(
                        r.DATE,
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind);
                }

                return new Email(
                    new Email.Identifiers
                    {
                        EmailID = r.EMAIL_ID,
                        ImapUID = (uint)r.IMAP_UID,
                        ImapUIDValidity = (uint)r.IMAP_UID_VALIDITY,
                        FolderFullName = r.FOLDER_FULLNAME,
                        MessageID = r.MESSAGE_ID,
                        OwnerAccountID = r.OWNER_ID,
                        InReplyTo = r.IN_REPLY_TO,
                    },
                    new Email.DataParts
                    {
                        Subject = r.SUBJECT,
                        Body = r.BODY,
                        From = r.FROM_ADDRESS,
                        To = r.TO_ADDRESS,
                        Date = date
                    }
                );
            }).ToList();
            return emails;
        }

        public async Task UpdateEmailBodyAsync(Email email)
        {
            var sql = @"
                UPDATE EMAILS
                SET BODY = @Body
                WHERE EMAIL_ID = @EmailId
                   OR MESSAGE_ID = @MessageId;
                ";

            using var connection = CreateConnection();

            await connection.ExecuteAsync(sql, new
            {
                EmailId = email.MessageIdentifiers.EmailID,
                MessageId = email.MessageIdentifiers.MessageID,
                Body = email.MessageParts.Body
            });
        }
        #endregion
    }
}
