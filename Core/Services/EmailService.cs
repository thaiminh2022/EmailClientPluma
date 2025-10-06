using EmailClientPluma.Core.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;
using System.Linq;

namespace EmailClientPluma.Core.Services
{
    interface IEmailService
    {
        Task<IEnumerable<Email>> FetchEmailHeadersAsync(Account acc);
        Task LoadEmailBodyAsync(Account acc, Email email);
        Task SendEmailAsync(Account acc, Email email);
    }
    internal class EmailService : IEmailService
    {
        public async Task<IEnumerable<Email>> FetchEmailHeadersAsync(Account acc)
        {
            using var imap = new ImapClient();
            await imap.ConnectAsync(GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect);
            var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
            await imap.AuthenticateAsync(oauth2);

            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);
            int takeAmount = Math.Min(10, inbox.Count);

            if (takeAmount <= 0)
            {
                await imap.DisconnectAsync(true);
                return [];
            }

            var last = inbox.Count;
            var start = Math.Max(0, last - takeAmount);
            var summaries = await inbox.FetchAsync(start, last - 1, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);

            List<Email> emails = [];
            foreach (var summary in summaries)
            {
                if (summary.Envelope is null || !summary.UniqueId.IsValid)
                {
                    continue;
                }

                var subject = string.IsNullOrEmpty(summary.Envelope.Subject) ? "(email have no subject)" : summary.Envelope.Subject;
                var from = summary.Envelope.From?.ToString() ?? string.Empty;
                var to = summary.Envelope.To?.ToString() ?? string.Empty;
                var uid = summary.UniqueId.Id;

                var email = new Email(acc.ProviderUID, subject, null, from, to, [], uid);
                emails.Add(email);
            }

            await imap.DisconnectAsync(true);

            return emails;
        }

        public async Task LoadEmailBodyAsync(Account acc, Email email)
        {
            if (email is null)
            {
                return;
            }

            if (email.IsBodyLoaded)
            {
                return;
            }

            if (email.ImapUid is null)
            {
                return;
            }

            using var imap = new ImapClient();
            await imap.ConnectAsync(GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect);
            var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
            await imap.AuthenticateAsync(oauth2);

            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var message = await inbox.GetMessageAsync(new UniqueId(email.ImapUid.Value));

            if (message is not null)
            {
                var body = message.HtmlBody ?? message.TextBody ?? "(email have no body)";
                email.Body = body;
                email.From = message.From?.ToString() ?? email.From;
                email.To = message.To?.Select(address => address.ToString()).ToArray() ?? Array.Empty<string>();
            }

            await imap.DisconnectAsync(true);
        }

        public async Task SendEmailAsync(Account acc, Email email)
        {
            // Constructing the email
            var message = ConstructEmail(email);
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(GetSmtpHostByProvider(acc.Provider), 587, SecureSocketOptions.StartTls);
            var oauth2 = new SaslMechanismOAuth2(acc.Email, acc.Credentials.SessionToken);
            //MessageBox.Show($"{acc.Email}\n{acc.Credentials.SessionToken}");
            await smtp.AuthenticateAsync(oauth2);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        MimeMessage ConstructEmail(Email email)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(email.From));
            InternetAddressList internetAddresses = new InternetAddressList();
            foreach (var item in email.To)
            {
                internetAddresses.Add(InternetAddress.Parse(item));
            }
            message.To.AddRange(internetAddresses);
            message.Subject = email.Subject;
            message.Body = new BodyBuilder
            {
                TextBody = email.Body
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
