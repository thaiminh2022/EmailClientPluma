using EmailClientPluma.Core.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace EmailClientPluma.Core.Services.Emailing
{
    interface IEmailService
    {
        Task FetchEmailHeaderAsync(Account acc);
        Task FetchEmailBodyAsync(Account acc, Email email);
        Task SendEmailAsync(Account acc, Email.OutgoingEmail email);

        Task PrefetchRecentBodiesAsync(Account acc, int maxToPrefetch = 30);
        Task<bool> FetchOlderHeadersAsync(Account acc, int window, CancellationToken token = default);
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
            var imap = new ImapClient()
            {
                CheckCertificateRevocation = false
            };

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
        private async Task<uint?> GetOldestSyncedUidAsync(Account acc, string folderFullName, uint uidValidity)
        {
            var emails = await _storageService.GetEmailsAsync(acc);

            var minUid = emails
               .Where(e =>
                   string.Equals(e.MessageIdentifiers.FolderFullName, folderFullName, StringComparison.OrdinalIgnoreCase)
                   && e.MessageIdentifiers.ImapUIDValidity == uidValidity)
               .Select(e => (uint?)e.MessageIdentifiers.ImapUID)
               .DefaultIfEmpty(null)
               .Min();

            return minUid;
        }

        static MimeMessage ConstructEmail(Account acc, Email.OutgoingEmail email)
        {
            var message = new MimeMessage();
            var address = MailboxAddress.Parse(acc.Email);
            address.Name = acc.DisplayName;
            message.From.Add(address);

            message.Subject = email.Subject;
            
            if (!string.IsNullOrEmpty(email.InReplyTo))
                message.InReplyTo = email.InReplyTo;

            InternetAddressList internetAddresses = [];

            if (!string.IsNullOrEmpty(email.ReplyTo))
            {
                foreach (var item in email.ReplyTo.Split(','))
                {
                    internetAddresses.Add(InternetAddress.Parse(item));
                }
                message.ReplyTo.AddRange(internetAddresses);
            }

            internetAddresses = [];
            foreach (var item in email.To.Split(','))
            {
                internetAddresses.Add(InternetAddress.Parse(item));
            }
            message.To.AddRange(internetAddresses);

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


                if (acc.Emails.Any(x => Helper.IsEmailEqual(x, email)))
                    continue;

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
            catch (Exception ex)
            {
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

            using var imap = await ConnectImapAsync(acc);
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
        public async Task<bool> FetchOlderHeadersAsync(Account acc, int window, CancellationToken token = default)
        {
            if (acc.NoMoreOlderEmail) return false;

            using var imap = await ConnectImapAsync(acc, token);
            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, token);

            if (inbox.Count == 0)
                return false;

            var serverUidValidity = inbox.UidValidity;

            // oldest cache locally
            uint? oldestUid = await GetOldestSyncedUidAsync(acc, inbox.FullName, serverUidValidity);

            // no emails, let the other function handle
            if (oldestUid is null)
                return false;

            if (oldestUid.Value <= 1)
            {
                acc.NoMoreOlderEmail = true;
                return false;
            }

            // Compute range of UIDs to fetch: [startUid .. endUid]
            uint endId = oldestUid.Value - 1; // just before our oldest
            uint startId = endId >= (uint)window
                ? endId - (uint)window + 1
                : 1u;

            var startUid = new UniqueId(startId);
            var endUid = new UniqueId(endId);
            var range = new UniqueIdRange(startUid, endUid);

            var summaries = await inbox.FetchAsync(
                range,
                MessageSummaryItems.Envelope |
                MessageSummaryItems.UniqueId |
                MessageSummaryItems.Size,
                token);

            if (summaries == null || summaries.Count == 0)
                return false;

            // Make sure we add them in ascending UID order
            foreach (var item in summaries.OrderBy(s => s.UniqueId.Id))
            {
                var email = Helper.CreateEmailFromSummary(acc, inbox, item);

                if (item.Size != null)
                    email.MessageParts.EmailSizeInKb = item.Size.Value / 1024.0;

                acc.Emails.Add(email);
                await _storageService.StoreEmailAsync(acc, email);
            }

            return true;
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
