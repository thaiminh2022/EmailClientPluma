using EmailClientPluma.Core.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Org.BouncyCastle.Crypto;
using System.Threading;
using System.Windows;

namespace EmailClientPluma.Core.Services.Emailing
{
    interface IEmailService
    {
        Task FetchEmailHeaderAsync(Account acc);
        Task FetchEmailBodyAsync(Account acc, Email email);
        Task SendEmailAsync(Account acc, Email.OutgoingEmail email);

        Task PrefetchRecentBodiesAsync(Account acc, int maxToPrefetch = 30);
    }
    internal class EmailService : IEmailService
    {
        readonly IStorageService _storageService;

        private const int INITIAL_HEADER_WINDOW = 20;

        public EmailService(IStorageService storageService)
        {
            _storageService = storageService;
        }

        #region Helper

        private async Task<ImapClient> ConnectImapAsync(Account acc, CancellationToken cancellationToken = default)
        {
            var imap = new ImapClient();

            await imap.ConnectAsync(
                Helper.GetImapHostByProvider(acc.Provider),
                993,
                SecureSocketOptions.SslOnConnect,
                cancellationToken);

            var oauth2 = new SaslMechanismOAuth2(acc.Email, acc.Credentials.SessionToken);
            await imap.AuthenticateAsync(oauth2, cancellationToken);

            return imap;

        }

        private async Task<uint?> GetLastSyncedUidAsync(Account acc, string folderFullName, uint uidValidity)
        {
            var emails = await _storageService.GetEmailsAsync(acc);

            var maxUid = emails
               .Where(e =>
                   string.Equals(e.MessageIdentifiers.FolderFullName, folderFullName, StringComparison.OrdinalIgnoreCase)
                   && e.MessageIdentifiers.ImapUIDValidity == uidValidity) // NEW
               .Select(e => (uint?)e.MessageIdentifiers.ImapUID)
               .DefaultIfEmpty(null)
               .Max();

            return maxUid;
        }
        static MimeMessage ConstructEmail(Account acc, Email.OutgoingEmail email)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(acc.Email));

            InternetAddressList internetAddresses = [];
            foreach (var item in email.To.Split(','))
            {
                internetAddresses.Add(InternetAddress.Parse(item));
            }
            message.To.AddRange(internetAddresses);

            message.Subject = email.Subject;
            if (email.ReplyTo != null)
            {
                message.ReplyTo.Add(MailboxAddress.Parse(email.ReplyTo));
            }

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = email.Body
            };

            foreach (var item in email.Attachments)
            {
                bodyBuilder.Attachments.Add(item.FileName, item.Content);
            }
            message.Body = bodyBuilder.ToMessageBody();

            message.Date = email.Date ?? DateTimeOffset.Now;
            return message;
        }

        #endregion

        #region Interface Methods
        public async Task FetchEmailHeaderAsync(Account acc)
        {
            // authenticating process
            using var imap = await ConnectImapAsync(acc);

            // open inbox
            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);


            if (inbox.Count == 0) // inbox is empty
            {
                await imap.DisconnectAsync(true);
                return;
            }

            // Get server UID validity
            var serverUidValidity = inbox.UidValidity;

            // Find last UID we have in local DB for this account + folder
            uint? lastUid = await GetLastSyncedUidAsync(acc, inbox.FullName, serverUidValidity);

            IList<IMessageSummary> summaries;

            // We dont have any emails of this account in the database
            // first fetch
            if (lastUid is null)
            {

                var take = Math.Min(INITIAL_HEADER_WINDOW, inbox.Count);
                var startSeq = Math.Max(0, inbox.Count - take);

                summaries = await inbox.FetchAsync(
                    startSeq,
                    inbox.Count - 1,
                    MessageSummaryItems.Envelope |
                    MessageSummaryItems.UniqueId |
                    MessageSummaryItems.Size);
            }
            else
            {
                if (!inbox.UidNext.HasValue)
                {
                    // Fallback: UidNext not provided, behave like initial with a smaller window
                    var take = Math.Min(INITIAL_HEADER_WINDOW, inbox.Count);
                    var startSeq = Math.Max(0, inbox.Count - take);

                    summaries = await inbox.FetchAsync(
                        startSeq,
                        inbox.Count - 1,
                        MessageSummaryItems.Envelope |
                        MessageSummaryItems.UniqueId |
                        MessageSummaryItems.Size);
                }
                else
                {
                    var maxUidOnServer = inbox.UidNext.Value.Id - 1u;
                    if (maxUidOnServer <= lastUid.Value)
                    {
                        // already up to date
                        await imap.DisconnectAsync(true);
                        return;
                    }

                    // Get the latest emails
                    var startUid = new UniqueId(lastUid.Value + 1);
                    var endUid = new UniqueId(maxUidOnServer);
                    var range = new UniqueIdRange(startUid, endUid);

                    summaries = await inbox.FetchAsync(
                        range,
                        MessageSummaryItems.Envelope |
                        MessageSummaryItems.UniqueId |
                        MessageSummaryItems.Size);
                }
            }

            //Map summaries to Email and save to storage
            foreach (var item in summaries)
            {
                var email = Helper.CreateEmailFromSummary(acc, inbox, item);

                if (item.Size != null)
                    email.MessageParts.EmailSizeInKb = item.Size.Value / 1024.0;

                acc.Emails.Add(email);
                await _storageService.StoreEmailAsync(acc, email);
            }

        }

        public async Task FetchEmailBodyAsync(Account acc, Email email)
        {
            // authenticating process
            using var imap = await ConnectImapAsync(acc);

            try
            {
                await FetchEmailBodyInternal(imap, email);

            }
            catch (Exception ex) {
                MessageBoxHelper.Error(ex.Message);
            }
        }

        private async Task FetchEmailBodyInternal(ImapClient imap, Email email)
        {
            // open the folder the message is in
            var folder = await imap.GetFolderAsync(email.MessageIdentifiers.FolderFullName);
            await folder.OpenAsync(FolderAccess.ReadOnly);

            var uniqueID = new UniqueId(email.MessageIdentifiers.ImapUID);


                var bodies = await folder.FetchAsync([uniqueID], MessageSummaryItems.BodyStructure);
                var bodyParts = bodies?.FirstOrDefault();
                if (bodies is null || bodyParts is null)
                {
                    email.MessageParts.Body = "(Unable to fetch body)";
                    return;
                }

                var chosen = bodyParts.HtmlBody ?? bodyParts.TextBody;
                if (chosen is null)
                {
                    email.MessageParts.Body = "(No Body)";
                }
                else
                {
                    var entity = await folder.GetBodyPartAsync(uniqueID, chosen);
                    if (entity is TextPart textPart)
                    {
                        email.MessageParts.Body = textPart.Text;
                    }
                    else
                    {
                        email.MessageParts.Body = "(No Body)";
                    }
                }

                await _storageService.UpdateEmailBodyAsync(email);
        }

        public async Task PrefetchRecentBodiesAsync(Account acc, int maxToPrefetch = 30)
        {
            var candidates = acc.Emails
                .Where(e => !e.BodyFetched)
                .OrderByDescending(e => e.MessageParts.Date)
                .Take(maxToPrefetch)
                .ToList();

            if (candidates.Count == 0)
                return;

            using var imap =await ConnectImapAsync(acc);
            try
            {
                foreach (var candidate in candidates)
                {
                    await FetchEmailBodyInternal(imap, candidate);
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Error(ex.Message);    
            }
        }

        public async Task SendEmailAsync(Account acc, Email.OutgoingEmail email)
        {
            // Constructing the email
            var message = ConstructEmail(acc, email);
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(Helper.GetSmtpHostByProvider(acc.Provider), 587, SecureSocketOptions.StartTls);
            var oauth2 = new SaslMechanismOAuth2(acc.Email, acc.Credentials.SessionToken);
            await smtp.AuthenticateAsync(oauth2);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
 
        #endregion



     
    }
}
