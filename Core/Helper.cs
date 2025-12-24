using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using MailKit;
using Microsoft.Win32;
using MimeKit;
using Org.BouncyCastle.Utilities;
using System.IO;
using System.Security.Cryptography;
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

        public async static Task<Attachment> CreateAttachmentFMP(Email email, MimePart part)
        {

            using var sha = SHA256.Create();

            DirectoryInfo dir = Directory.CreateDirectory(
                Path.Combine(Helper.DataFolder, "Attachments")
            );

            string tempPath = Path.Combine(dir.FullName, Guid.NewGuid().ToString("N") + ".tmp");

            // 1️ Stream decode → disk + hasher
            await using (var fileStream = File.Create(tempPath))
            await using (var crypto = new CryptoStream(fileStream, sha, CryptoStreamMode.Write))
            {
                await part.Content.DecodeToAsync(crypto);
                await crypto.FlushFinalBlockAsync();
            }

            // 2️ Finalize SHA256 and build storage key
            string storageKey = Convert.ToHexString(sha.Hash!);
            string finalPath = Path.Combine(dir.FullName, storageKey);

            // 3️ Deduplicate
            if (!File.Exists(finalPath))
                File.Move(tempPath, finalPath);
            else
                File.Delete(tempPath);

            // 4️ Build DB record
            return new Attachment
            {
                OwnerEmailID = email.MessageIdentifiers.EmailId,
                FileName = part.FileName,
                MimeType = part.ContentType.MimeType,
                Size = new FileInfo(finalPath).Length,
                StorageKey = storageKey
            };
        }

        public static async Task EventDownloadAttachmentAsync(Attachment att)
        {
            SaveFileDialog sfd = new SaveFileDialog
            {
                FileName = att.FileName,
                Filter = $"{att.FileName}|*.*"
            };

            if (sfd.ShowDialog() != true)
                return;

            byte[] storedBytes = await File.ReadAllBytesAsync(att.FilePath);
            await File.WriteAllBytesAsync(sfd.FileName, storedBytes);
        }
    }
}
