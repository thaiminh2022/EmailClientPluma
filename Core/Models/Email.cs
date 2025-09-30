namespace EmailClientPluma.Core.Models
{
    internal class Email
    {
        public int EmailID { get; set; } // set by database
        public int OwnerAccountID { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }

        public string From { get; set; }
        public string[] To { get; set; }

        public IEnumerable<Attachment> Attachments { get; set; }

        public Email(int ownerAccountID, string subject, string body, string from, string to, IEnumerable<Attachment> attachments)
        {
            OwnerAccountID = ownerAccountID;
            Subject = subject;
            Body = body;
            From = from;
            To = to.Split(';');
            Attachments = attachments;
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
