using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using MailKit;
using MimeKit;
using System.IO;
using System.Windows.Media;

namespace EmailClientPluma.Core
{
    internal static class Helper
    {
        public static string DataFolder => GetDataFolder();
        public static string DatabasePath => Path.Combine(DataFolder, "pluma.db");

        static string GetDataFolder()
        {
            var path = Path.Combine(Environment.GetFolderPath(
                                                    Environment.SpecialFolder.ApplicationData), "Pluma");
            Directory.CreateDirectory(path);
            return path;
        }
        public static string GetSmtpHostByProvider(Provider prod)
        {
            return prod switch
            {
                Provider.Google => "smtp.gmail.com",
                _ => throw new NotImplementedException()
            };
        }
        public static string GetImapHostByProvider(Provider prod)
        {
            return prod switch
            {
                Provider.Google => "imap.gmail.com",
                _ => throw new NotImplementedException()
            };
        }
        public static Email CreateEmailFromSummary(Account acc, IMailFolder inbox, IMessageSummary summary)
        {
            var env = summary.Envelope;
            var uniqueId = summary.UniqueId.Id;
            var messageId = env.MessageId;
            var uidValidity = inbox.UidValidity;
            var inReplyTo = env.InReplyTo;


            var email = new Email(
                new Email.Identifiers
                {
                    ImapUid = uniqueId,
                    ImapUidValidity = uidValidity,
                    FolderFullName = inbox.FullName,
                    MessageId = messageId,
                    OwnerAccountId = acc.ProviderUID,
                    InReplyTo = inReplyTo,
                    Flags = summary.Flags ?? MessageFlags.None

                },
                new Email.DataParts
                {
                    Subject = env.Subject ?? "(No Subject)",
                    From = env.From.ToString(),
                    To = env.To.ToString(),
                    Date = env.Date
                }
            );

            email.Labels.Add(EmailLabel.All);
            // is the email sent or received?
            if (env.From.Contains(new MailboxAddress(acc.DisplayName, acc.Email)))
            {
                // im the sender
                email.Labels.Add(EmailLabel.Sent);
            }
            else
            {
                // im the receiver
                email.Labels.Add(EmailLabel.Inbox);
            }

            return email;
        }

        public static bool IsEmailEqual(Email a, Email b)
        {
            return string.Equals(a.MessageIdentifiers.OwnerAccountId, b.MessageIdentifiers.OwnerAccountId) &&
                   string.Equals(a.MessageIdentifiers.MessageId, b.MessageIdentifiers.MessageId);
        }

        public static Color ColorFromARGB(int argb)
        {
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >> 8) & 0xFF);
            byte b = (byte)(argb & 0xFF);
            return Color.FromArgb(a, r, g, b);
        }

        public static int ColorToARGB(Color c)
        {
            return (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
        }
    }
}
