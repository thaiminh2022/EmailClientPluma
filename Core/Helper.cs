using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using MailKit;
using System;
using System.IO;

namespace EmailClientPluma.Core
{
    internal static class Helper
    {
        public static string DataFolder => GetDataFolder();
        public static string DatabasePath => Path.Combine(DataFolder, "pluma.db");
        public static string ClientSecretPathDev = @"secrets/secret.json";

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
            var uniqueID = summary.UniqueId.Id;
            var messageID = env.MessageId;
            var uidValidity = inbox.UidValidity;
            var inReplyTo = env.InReplyTo;

            var email = new Email(
                new Email.Identifiers
                {
                    ImapUID = uniqueID,
                    ImapUIDValidity = uidValidity,
                    FolderFullName = inbox.FullName,
                    MessageID = messageID,
                    OwnerAccountID = acc.ProviderUID,
                    InReplyTo = inReplyTo,

                },
                new Email.DataParts
                {
                    Subject = env.Subject ?? "(No Subject)",
                    From = env.From.ToString(),
                    To = env.To.ToString(),
                    Date = env.Date
                }
            );

            return email;
        }
    }
}
