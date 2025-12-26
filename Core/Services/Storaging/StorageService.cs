using Dapper;
using EmailClientPluma.Core.Models;

using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Storaging;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;
using System.Windows;


namespace EmailClientPluma.Core.Services.Storaging
{
    interface IStorageService
    {
        //ACCOUNTS
        Task<IEnumerable<Account>> GetAccountsAsync();
        Task<int> StoreAccountAsync(Account account);
        Task RemoveAccountAsync(Account account);

        //EMAILS
        Task<IEnumerable<Email>> GetEmailsAsync(Account acc);
        Task StoreEmailAsync(Account acc);
        Task StoreEmailAsync(Account acc, Email mail);
        Task UpdateEmailBodyAsync(Email email);

        //LABELS
        Task<IEnumerable<EmailLabel>> GetLabelsAsync(Account acc);
        Task StoreLabelAsync(Account acc);
        Task StoreLabelsAsync(Email mail);
        Task DeleteLabelAsync(EmailLabel label);
        Task DeleteEmailLabelAsync(EmailLabel label, Email email);

        //ATTACHMENTS
        Task<IEnumerable<Attachment>> GetAttachmentsAsync(Email email);
        Task StoreAttachmentsAsync(Email email);
        Task StoreAttachmentsAsync(Email email, IEnumerable<Attachment> attachments);
        Task<bool> DeleteAttachmentAsync(Attachment attachment);

    }
    internal class StorageService : IStorageService
    {
        private readonly AccountStorage _accountStorage;
        private readonly EmailStorage _emailStorage;
        private readonly LabelStorage _labelStorage;
        private readonly AttachmentStorage _attachmentStorage;

        public StorageService()
        {
            var connectionString = $"Data Source={Helper.DatabasePath}";
            var tokenStore = new SQLiteDataStore(Helper.DatabasePath);

            _accountStorage = new AccountStorage(tokenStore, connectionString);
            _emailStorage = new EmailStorage(connectionString);
            _labelStorage = new LabelStorage(connectionString);
            _attachmentStorage = new AttachmentStorage(connectionString);

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

                var attachments = await _attachmentStorage.GetAttachmentsAsync(mail);
                mail.MessageParts.Attachments = attachments;
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
        public async Task<IEnumerable<Attachment>> GetAttachmentsAsync(Email email)
        {
            return await _attachmentStorage.GetAttachmentsAsync(email);
        }
        public async Task StoreAttachmentsAsync(Email email, IEnumerable<Attachment> attachments)
        {
            await _attachmentStorage.StoreAttachmentsInternal(email, attachments);
        }

        public async Task StoreAttachmentsAsync(Email email) => await StoreAttachmentsAsync(email, email.MessageParts.Attachments);
        public async Task<bool> DeleteAttachmentAsync(Attachment attachment)
        {
            return await _attachmentStorage.DeleteAttachmentInternal(attachment);
        }

        #endregion
    }
}
