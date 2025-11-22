namespace EmailClientPluma.Core.Models
{
    internal class Email : ObserableObject
    {
        public record Identifiers
        {
            public int EmailID { get; set; } // set by database
            required public uint ImapUID { get; set; }
            required public uint ImapUIDValidity { get; set; }
            required public string FolderFullName { get; set; }
            required public string? MessageID { get; set; }
            required public string OwnerAccountID { get; set; }
            required public string? InReplyTo { get; set; }
        }
        public class DataParts : ObserableObject
        {
            required public string Subject { get; set; }

            private string? _body;
            public string? Body
            {
                get { return _body; }
                set
                {
                    _body = value;
                    OnPropertyChanges();
                }
            }

            required public string From { get; set; }
            required public string To { get; set; }
            required public DateTimeOffset? Date { get; set; }

            public double EmailSizeInKb { get; set; } = 0;

            public IEnumerable<Attachment> Attachments { get; set; } = [];
        }

        internal class OutgoingEmail : DataParts
        {
            required public string? ReplyTo { get; set; }

        }


        public Identifiers MessageIdentifiers { get; set; }
        public DataParts MessageParts { get; set; }

        public bool BodyFetched => !string.IsNullOrEmpty(MessageParts.Body);

        public Email(Identifiers messageIdentifiers, DataParts messagesParts)
        {
            MessageIdentifiers = messageIdentifiers;
            MessageParts = messagesParts;
        }
    }



    internal record Attachment
    {
        public int AttachmentID { get; set; } // set by database    
        required public int OwnerEmailID { get; set; }
        required public string FileName { get; set; }
        required public byte[] Content { get; set; }

    }

}
