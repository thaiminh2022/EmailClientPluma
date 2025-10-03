using EmailClientPluma.Core.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace EmailClientPluma.Core.Services
{
    interface IEmailService
    {
        Task<IEnumerable<Email>> FetchEmailAsync(Account acc);
        Task SendEmailAsync(Account acc, Email email);
    }
    internal class EmailService : IEmailService
    {
        public async Task<IEnumerable<Email>> FetchEmailAsync(Account acc)
        {
            using var imap = new ImapClient();
            await imap.ConnectAsync(GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect);
            var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
            await imap.AuthenticateAsync(oauth2);


            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);
            int takeAmount = Math.Min(10, inbox.Count);


            var last = inbox.Count;
            var start = System.Math.Max(0, last - takeAmount);
            List<Email> emails = [];
            for (int i = start; i < last; i++)
            {
                var msg = await inbox.GetMessageAsync(i);

                if (msg is not null)
                {
                    var body = msg.HtmlBody ?? msg.TextBody;
                    body ??= "(email have no body)";

                    var m = new Email(acc.ProviderUID, msg.Subject, msg.TextBody, msg.From.ToString(), msg.To.ToString(), []);
                    emails.Add(m);
                }

            }

            await imap.DisconnectAsync(true);

            return emails;
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
