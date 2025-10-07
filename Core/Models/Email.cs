namespace EmailClientPluma.Core.Models
{
    internal class Email : ObserableObject
    {
        public int EmailID { get; set; } // set by database
        public uint ImapUID { get; set; }
        public string MessageID { get; set; }
        public string OwnerAccountID { get; set; }
        public string Subject { get; set; }



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

        public string From { get; set; }
        public string To { get; set; }

        public IEnumerable<Attachment> Attachments { get; set; } = [];

        public Email(uint imapUID, string messageID, string ownerAccountID, string subject, string from, string to)
        {
            ImapUID = imapUID;
            MessageID = messageID;
            OwnerAccountID = ownerAccountID;
            Subject = subject;
            From = from;
            To = to;
        }
    }

    internal class Attachment
    {
        public int AttachmentID { get; set; } // set by database    
        public int OwnerEmailID { get; set; }
        public byte[] Content { get; set; }

        public Attachment(int ownerEmailID, byte[] content)
        {
            OwnerEmailID = ownerEmailID;
            Content = content;
        }
    }
}
