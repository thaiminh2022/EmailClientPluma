using EmailClientPluma.Core.Models;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Data.Sqlite;
using Microsoft.Web.WebView2.Core;
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
    internal class StorageService : IStorageService
    {
        readonly string _connectionString;
        readonly SQLiteDataStore _tokenStore;


        public StorageService()
        {
            _connectionString = $"Data Source={Helper.DatabasePath}";
            _tokenStore = new SQLiteDataStore(Helper.DatabasePath);
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

            // user storage
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

            // email storage
            command.CommandText = @"
                                    CREATE TABLE IF NOT EXISTS EMAILS (
                                    EMAIL_ID           INTEGER PRIMARY KEY AUTOINCREMENT,  -- DB surrogate key

                                    -- IDENTIFIERS
                                    IMAP_UID           INTEGER NOT NULL,                   -- uint -> INTEGER
                                    IMAP_UID_VALIDITY  INTEGER NOT NULL,                   -- uint -> INTEGER
                                    FOLDER_FULLNAME    TEXT    NOT NULL,
                                    MESSAGE_ID         TEXT UNIQUE,                        -- nullable
                                    OWNER_ID           TEXT    NOT NULL,
                                    IN_REPLY_TO        TEXT,                               -- nullable

                                    -- DATA PARTS
                                    SUBJECT            TEXT    NOT NULL,
                                    BODY               TEXT,                               -- nullable (lazy-loaded)
                                    FROM_ADDRESS       TEXT    NOT NULL,
                                    TO_ADDRESS         TEXT    NOT NULL,
                                    DATE               TEXT,                               -- nullable

                                    FOREIGN KEY (OWNER_ID) REFERENCES ACCOUNTS(PROVIDER_UID) ON DELETE CASCADE
);
                                    ";
            command.ExecuteNonQuery();
        }

        public async Task StoreEmailsInternal(Account acc, IEnumerable<Email> mails)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            command.CommandText = @"INSERT INTO EMAILS (
                                    IMAP_UID, IMAP_UID_VALIDITY, FOLDER_FULLNAME, MESSAGE_ID, OWNER_ID, IN_REPLY_TO,
                                    SUBJECT, BODY, FROM_ADDRESS, TO_ADDRESS, DATE
                                ) VALUES (
                                    $imap_uid, $imap_uid_validity, $folder_fullname, $message_id, $owner_id, $in_reply_to,
                                    $subject, $body, $from, $to, $date
                                ) ON CONFLICT (MESSAGE_ID)
                                  DO UPDATE SET 
                                    SUBJECT = excluded.SUBJECT,
                                    BODY = COALESCE(excluded.BODY, EMAILS.BODY),
                                    FROM_ADDRESS = excluded.FROM_ADDRESS,
                                    TO_ADDRESS = excluded.TO_ADDRESS,
                                    DATE = excluded.DATE
                                   ";
            foreach (var item in mails)
            {
                var msgPart = item.MessageParts;
                var msgId = item.MessageIdentifiers;
                // IDS
                command.Parameters.AddWithValue("$imap_uid", msgId.ImapUID);
                command.Parameters.AddWithValue("$imap_uid_validity", msgId.ImapUIDValidity);
                command.Parameters.AddWithValue("$folder_fullname", msgId.FolderFullName);
                command.Parameters.AddWithValue("$message_id", msgId.MessageID);
                command.Parameters.AddWithValue("$owner_id", acc.ProviderUID);
                command.Parameters.AddWithValue("$in_reply_to", DbNullOrValue(msgId.InReplyTo));

                // PARTS
                command.Parameters.AddWithValue("$subject", msgPart.Subject);
                command.Parameters.AddWithValue("$body", DbNullOrValue(msgPart.Body));
                command.Parameters.AddWithValue("$from", msgPart.From);
                command.Parameters.AddWithValue("$to", msgPart.To);
                command.Parameters.AddWithValue("$date", msgPart.Date?.ToString("o"));

                try
                {
                    await command.ExecuteNonQueryAsync();

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                command.Parameters.Clear();
            }
        }


        public async Task StoreEmailAsync(Account acc)
        {
            await StoreEmailsInternal(acc, acc.Emails);
        }

        public async Task StoreEmailAsync(Account acc, Email mail)
        {
            await StoreEmailsInternal(acc, [mail]);
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
                var imapUID = reader.GetFieldValue<uint>(1);
                var imapUIDValidity = reader.GetFieldValue<uint>(2);
                var folderFullName = reader.GetString(3);
                var messageID = reader.IsDBNull(4) ? null : reader.GetString(4);
                var emailAccountOwnerID = reader.GetString(5);
                var inReplyTo = reader.IsDBNull(6) ? null :  reader.GetString(6);

                var subject = reader.GetString(7);
                var body = reader.IsDBNull(8) ? null : reader.GetString(8);
                var from = reader.GetString(9);
                var to = reader.GetString(10);
                DateTimeOffset? date = reader.IsDBNull(11) ? null : DateTimeOffset.Parse(reader.GetString(11));
                var email = new Email(
                    new Email.Identifiers
                    {
                        EmailID = emailID,
                        ImapUID = imapUID,
                        ImapUIDValidity = imapUIDValidity,
                        FolderFullName = folderFullName,
                        MessageID = messageID,
                        OwnerAccountID = emailAccountOwnerID,
                        InReplyTo = inReplyTo,  
                    },
                    new Email.DataParts
                    {
                        Subject = subject,
                        Body = body,
                        From = from,
                        To = to,
                        Date = date
                    }
                );
                emails.Add(email);
            }
            return emails;
        }

        public async Task UpdateEmailBodyAsync(Email email)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"UPDATE EMAILS
                                    SET BODY = $body
                                    WHERE EMAIL_ID = $email_id 
                                    OR MESSAGE_ID = $message_id;                  
                                    ";

            command.Parameters.AddWithValue("$email_id", email.MessageIdentifiers.EmailID);
            command.Parameters.AddWithValue("$message_id", email.MessageIdentifiers.MessageID);
            command.Parameters.AddWithValue("$body", email.MessageParts.Body);
            await command.ExecuteNonQueryAsync();
        }

        private object DbNullOrValue<T>(T? value)
        {
            if (value is null)
                return DBNull.Value;
            return value;
        }

    }
}
