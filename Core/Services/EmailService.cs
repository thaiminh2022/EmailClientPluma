using EmailClientPluma.Core.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Windows;

namespace EmailClientPluma.Core.Services
{
    interface IEmailService
    {
        Task FetchEmailHeaderAsync(Account acc);
        Task FetchEmailBodyAsync(Account acc, Email email);
        Task SendEmailAsync(Account acc, Email.OutgoingEmail email);
    }
    internal class EmailService : IEmailService
    {
        readonly IStorageService _storageService;

        public EmailService(IStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task FetchEmailHeaderAsync(Account acc)
        {
            // authenticating process
            using var imap = new ImapClient();
            await imap.ConnectAsync(Helper.GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect);
            var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
            await imap.AuthenticateAsync(oauth2);

            // getting headers process
            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);


            if (inbox.Count == 0) // inbox is empty
            {
                await imap.DisconnectAsync(true);
                return;
            }

            // Fetch latest 20 messages' envelope (headers summary)
            int take = Math.Min(20, inbox.Count);
            int start = Math.Max(0, inbox.Count - take);

            var summaries = await inbox.FetchAsync(start, inbox.Count - 1, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);

            foreach (var item in summaries)
            {
                var email = Helper.CreateEmailFromSummary(acc, inbox, item);

                // set size in KB
                email.MessageParts.EmailSizeInKb = (double)item.Size / 1024.0;

                acc.Emails.Add(email);
            }
            await imap.DisconnectAsync(true);
            await _storageService.StoreEmailAsync(acc);
        }
        public async Task FetchEmailBodyAsync(Account acc, Email email)
        {
            // authenticating process
            using var imap = new ImapClient();
            await imap.ConnectAsync(Helper.GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect);
            var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
            await imap.AuthenticateAsync(oauth2);

            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var uniqueID = new UniqueId(email.MessageIdentifiers.ImapUID);

            try
            {
                var bodies = await inbox.FetchAsync([uniqueID], MessageSummaryItems.BodyStructure);
                var bodyParts = bodies?.FirstOrDefault();
                if (bodies is null || bodyParts is null)
                {
                    email.MessageParts.Body = "(Unable to fetch body)";
                    return;
                }

                var chosen = bodyParts.HtmlBody ?? bodyParts.TextBody;
                var entity = await inbox.GetBodyPartAsync(uniqueID, chosen);
                if (entity is TextPart textPart)
                {
                    email.MessageParts.Body = textPart.Text;
                }
                else
                {
                    email.MessageParts.Body = "(No Body)";
                }
                await _storageService.UpdateEmailBodyAsync(email);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        public async Task SendEmailAsync(Account acc, Email.OutgoingEmail email)
        {
            // Constructing the email
            var message = ConstructEmail(acc, email);
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(Helper.GetSmtpHostByProvider(acc.Provider), 587, SecureSocketOptions.StartTls);
            var oauth2 = new SaslMechanismOAuth2(acc.Email, acc.Credentials.SessionToken);
            //MessageBox.Show($"{acc.Email}\n{acc.Credentials.SessionToken}");
            await smtp.AuthenticateAsync(oauth2);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
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
            if (email.ReplyTo != null) {
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
    }
}
