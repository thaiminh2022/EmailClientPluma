using MailKit;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Shapes;

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
        public int AttachmentID { get; set; }
        public int OwnerEmailID { get; set; }

        public string FileName { get; set; } = "";
        public string MimeType { get; set; } = "";
        public long Size { get; set; }

        public string FilePath
        {
            get
            {
                DirectoryInfo directory = Directory.CreateDirectory(
                    System.IO.Path.Combine(Helper.DataFolder, "Attachments"));

                return System.IO.Path.Combine(directory.FullName, StorageKey);
            }
        }

        
        public byte[] Content => File.ReadAllBytes(FilePath);
        public string StorageKey { get; set; } = "";

    }

}
