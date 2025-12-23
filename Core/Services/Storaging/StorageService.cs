using EmailClientPluma.Core.Models;
using Dapper;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Storaging;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Windows;
using System.Security.Cryptography;


namespace EmailClientPluma.Core.Services.Storaging
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
        Task StoreAttachmentsFromEmail(Email email);
        Task<IEnumerable<Attachment>> GetAttachmentsFromEmail(Email email); 

        Task<IEnumerable<EmailLabel>> GetLabelsAsync(Account acc);
        Task StoreLabelAsync(Account acc);
        Task StoreLabelsAsync(Email mail);
        Task DeleteLabelAsync(EmailLabel label);
        Task DeleteEmailLabelAsync(EmailLabel label, Email email);

    }
    internal class StorageService : IStorageService
    {
        private readonly AccountStorage _accountStorage;
        private readonly EmailStorage _emailStorage;
        private readonly LabelStorage _labelStorage;

        public StorageService()
        {
            var connectionString = $"Data Source={Helper.DatabasePath}";
            var tokenStore = new SQLiteDataStore(Helper.DatabasePath);

            _accountStorage = new AccountStorage(tokenStore, connectionString);
            _emailStorage = new EmailStorage(connectionString);
            _labelStorage = new LabelStorage(connectionString);

            var migrator = new StorageMigrator(connectionString);
            migrator.Migrate();
        }


        #region Accounts

        public async Task<IEnumerable<Account>> GetAccountsAsync()
        {
            return await _accountStorage.GetAccountsAsync();
        }
        public async Task<int> StoreAccountAsync(Account account)
        {
            var affected = await _accountStorage.StoreAccountAsync(account);
            await _labelStorage.StoreDefaultLabel(account);
            return affected;
        }

        public async Task RemoveAccountAsync(Account account)
        {
            await _accountStorage.RemoveAccountAsync(account);
        }

        #endregion

        #region Emails
        public async Task StoreEmailsInternal(Account acc, IEnumerable<Email> mails)
        {
            var mailAsync = mails.ToList();

            // Store emails
            await _emailStorage.StoreEmailsInternal(acc, mailAsync);

            foreach (var mail in mailAsync)
            {
                await _labelStorage.StoreLabelsAsync(mail);
            }

        }
        public async Task StoreEmailAsync(Account acc) => await StoreEmailsInternal(acc, acc.Emails);
        public async Task StoreEmailAsync(Account acc, Email mail) => await StoreEmailsInternal(acc, [mail]);

        public async Task<IEnumerable<Email>> GetEmailsAsync(Account acc)
        {
            var emails = await _emailStorage.GetEmailsAsync(acc);

            foreach (var mail in emails)
            {
                var labels = await _labelStorage.GetLabelsAsync(mail);
                mail.Labels = new(labels);
            }

            return emails;
        }
        public async Task UpdateEmailBodyAsync(Email email)
        {
            await _emailStorage.UpdateEmailBodyAsync(email);
        }


        #endregion

        #region Labels
        public async Task<IEnumerable<EmailLabel>> GetLabelsAsync(Account acc)
        {
            return await _labelStorage.GetLabelsAsync(acc);
        }

        public async Task StoreLabelAsync(Account acc)
        {
            await _labelStorage.StoreLabelAsync(acc);
        }

        public async Task StoreLabelsAsync(Email mail)
        {
            await _labelStorage.StoreLabelsAsync(mail);
        }

        public async Task DeleteLabelAsync(EmailLabel label)
        {
            await _labelStorage.DeleteLabelAsync(label);
        }

        public async Task DeleteEmailLabelAsync(EmailLabel label, Email email)
        {
            await _labelStorage.DeleteEmailLabelAsync(label, email);
        }

        #endregion

        #region Attachments
        public async Task StoreAttachmentsFromEmail (Email email)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            
            foreach (var part in email.MessageParts.Attachments)
            {
                // 1. Decode bytes
                
                byte[] bytes = part.Content;

                // 2. Generate storage key
                string storageKey = Convert.ToHexString(
                    SHA256.HashData(bytes));


                // 3. Save to vault (dedup)
                DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(Helper.DataFolder,"Attachments"));
                string path = Path.Combine(directory.FullName, storageKey);

                if (!File.Exists(path))
                    await File.WriteAllBytesAsync(path, bytes);

                // 4. Insert metadata row
                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                """
                INSERT INTO ATTACHMENTS
                (EMAIL_ID, FILENAME, MIMETYPE, SIZE, STORAGE_KEY, CREATEDUTC)
                VALUES (@email,@name,@mime,@size,@key,@utc);
                """;

                cmd.Parameters.AddWithValue("@email", email.MessageIdentifiers.EmailID);
                cmd.Parameters.AddWithValue("@name", part.FileName);
                cmd.Parameters.AddWithValue("@mime", part.MimeType);
                cmd.Parameters.AddWithValue("@size", bytes.Length);
                cmd.Parameters.AddWithValue("@key", storageKey);
                cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<IEnumerable<Attachment>> GetAttachmentsFromEmail(Email email)
        {
            using var connection = CreateConnection();
            var sql = @"
                        SELECT ATTACHMENT_ID,
                               EMAIL_ID,
                               FILENAME,
                               MIMETYPE,
                               SIZE,
                               STORAGE_KEY,
                               CREATEDUTC
                        FROM ATTACHMENTS
                        WHERE EMAILID = @EmailId
                       ";
            var rows = await connection.QueryAsync<AttachmentRow>(sql, new { EmailId = email.MessageIdentifiers.EmailID });
            var attachments = rows.Select(r =>
            {
                return new Attachment
                {
                    AttachmentID = r.ATTACHMENT_ID,
                    OwnerEmailID = r.EMAIL_ID,
                    FileName = r.FILENAME,
                    MimeType = r.MIMETYPE,
                    Content = File.ReadAllBytes(
                        Path.Combine(Helper.DataFolder, r.STORAGE_KEY)),
                };
            }).ToList();
            return attachments;
        }

        #endregion
    }
}
