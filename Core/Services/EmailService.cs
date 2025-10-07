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
        Task SendEmailAsync(Account acc, Email email);
    }
    internal class EmailService : IEmailService
    {
        public async Task FetchEmailHeaderAsync(Account acc)
        {
            // authenticating process
            using var imap = new ImapClient();
            await imap.ConnectAsync(GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect);
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
                var env = item.Envelope;
                var uniqueID = item.UniqueId.Id;
                var messageID = env.MessageId;
                var uidValidity = inbox.UidValidity;

                var email = new Email(
                    new Email.Identifiers
                    {
                        ImapUID = uniqueID,
                        ImapUIDValidity = uidValidity,
                        FolderFullName = inbox.FullName,
                        MessageID = messageID,
                        OwnerAccountID = acc.ProviderUID
                    },
                    new Email.DataParts
                    {
                        Subject = env.Subject ?? "(No Subject)",
                        From = env.From.ToString(),
                        To = env.To.ToString(),
                    }
                );


                acc.Emails.Add(email);
            }
            await imap.DisconnectAsync(true);
        }
        public async Task FetchEmailBodyAsync(Account acc, Email email)
        {
            // authenticating process
            using var imap = new ImapClient();
            await imap.ConnectAsync(GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect);
            var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
            await imap.AuthenticateAsync(oauth2);

            var folder = await imap.GetFolderAsync(email.MessageIdentifiers.FolderFullName);
            if (folder is null)
                return;

            var uids = new List<UniqueId>()
            {
                new(email.MessageIdentifiers.ImapUID)
            };
            try
            {
                await folder.OpenAsync(FolderAccess.ReadOnly);
                var mailBodies = await folder.FetchAsync(uids, MessageSummaryItems.BodyStructure, default);
                var bodyParts = mailBodies?.FirstOrDefault();
                if (bodyParts == null)
                    return;


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        public async Task SendEmailAsync(Account acc, Email email)
        {
            // Constructing the email
            var message = ConstructEmail(acc, email);
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(GetSmtpHostByProvider(acc.Provider), 587, SecureSocketOptions.StartTls);
            var oauth2 = new SaslMechanismOAuth2(acc.Email, acc.Credentials.SessionToken);
            //MessageBox.Show($"{acc.Email}\n{acc.Credentials.SessionToken}");
            await smtp.AuthenticateAsync(oauth2);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        MimeMessage ConstructEmail(Account acc, Email email)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(acc.Email));
            InternetAddressList internetAddresses = new InternetAddressList();
            foreach (var item in email.MessageParts.To.Split(','))
            {
                internetAddresses.Add(InternetAddress.Parse(item));
            }
            message.To.AddRange(internetAddresses);
            message.Subject = email.MessageParts.Subject;
            message.Body = new BodyBuilder
            {
                TextBody = email.MessageParts.Body
            }.ToMessageBody();
            return message;
        }

        string GetSmtpHostByProvider(Provider prod)
        {
            return prod switch
            {
                Provider.Google => "smtp.gmail.com",
                _ => throw new NotImplementedException()
            };
        }

        string GetImapHostByProvider(Provider prod)
        {
            return prod switch
            {
                Provider.Google => "imap.gmail.com",
                _ => throw new NotImplementedException()
            };
        }


    }
}
