using EmailClientPluma.Core.Services.Emailing;
using MailKit;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using static EmailClientPluma.Core.Models.Email;

namespace EmailClientPluma.Core.Models
{
    internal class Email : ObserableObject
    {
        public record Identifiers
        {
            public int EmailId { get; set; } // set by database
            public required uint ImapUid { get; set; }
            public required uint ImapUidValidity { get; set; }
            public required string FolderFullName { get; set; }
            public required string? MessageId { get; set; }
            public required string OwnerAccountId { get; set; }
            public required string? InReplyTo { get; set; }

            public required MessageFlags Flags { get; set; }
        }
        public class DataParts : ObserableObject
        {
            public required string Subject { get; set; }

            private string? _body;
            public string? Body
            {
                get => _body;
                set
                {
                    _body = value;
                    OnPropertyChanges();
                }
            }

            public required string From { get; set; }
            public required string To { get; set; }
            public required DateTimeOffset? Date { get; set; }
            public string DateDisplay => Date?.ToLocalTime().DateTime.ToString("g", CultureInfo.CurrentCulture) ?? string.Empty;

            public double EmailSizeInKb { get; set; } = 0;

            public IEnumerable<Attachment> Attachments { get; set; } = [];
        }

        internal class OutgoingEmail : DataParts
        {
            required public string? InReplyTo { get; set; }
            required public string? ReplyTo { get; set; }
        }


        public Identifiers MessageIdentifiers { get; set; }
        public DataParts MessageParts { get; set; }

        public ObservableCollection<EmailLabel> Labels { get; set; }

        public bool BodyFetched => !string.IsNullOrEmpty(MessageParts.Body);
        public bool Seen => MessageIdentifiers.Flags.HasFlag(MessageFlags.Seen);

        public string LabelsDisplay => string.Join("; ", Labels.Select(x => x.Name));

        public Email(Identifiers messageIdentifiers, DataParts messagesParts)
        {
            MessageIdentifiers = messageIdentifiers;
            MessageParts = messagesParts;

            // add default labels when init
            Labels = [];
        }
    }



    internal record Attachment
    {
        // ---------- identity ----------
        public int AttachmentID { get; set; }
        public bool IsOutgoing { get; set; }

        public int OwnerEmailID { get; init; }

        // ---------- metadata ----------
        public string FileName { get; init; } = "";
        public string MimeType { get; init; } = "";
        public long? Size { get; init; }

        // ---------- IMAP identity (incoming only) ----------
        public uint? ImapUid { get; set; }
        public uint? ImapUidValidity { get; set; }
        public string? FolderFullName { get; set; }
        public string? MimePartPath { get;  set; }

        // ---------- cache ----------
        public string FilePath =>
            Path.Combine(
                Helper.DataFolder,
                IsOutgoing ? "OutgoingAttachments" : "Attachments",
                AttachmentID.ToString()
            );

        public string FusedFileName => $"{AttachmentID}-{FileName}";
        public string FusedFilePath =>
            Path.Combine(
                Helper.DataFolder,
                IsOutgoing ? "OutgoingAttachments" : "Attachments",
                FusedFileName
            );

        public byte[]? Content
        {
            get
            {
                if(File.Exists(FilePath))
                    return File.ReadAllBytes(FilePath);
                else if(File.Exists(FusedFilePath))
                    return File.ReadAllBytes(FusedFilePath);
                else
                    return null;
            }
        }
          

        
    }
        
 }
