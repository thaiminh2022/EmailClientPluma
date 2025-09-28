namespace EmailClientPluma.Core.Models
{
    internal class Email {
        public string Subject { get; set; }
        public string BodyText { get; set; }  
        public string BodyHtml { get; set; }
        public string From { get; set; }       
        public List<string> To { get; set; } = [];
        public List<Attachment> Attachments { get; set; } = [];
        
    }

    internal class SentEmail : Email
    {
        public List<string> Cc { get; set; } = [];
        public List<string> Bcc { get; set; } = [];
    }

    internal class ReceiveEmail : Email
    {
        public string Folder { get; set; }
    }

    internal class Attachment
    {

    }
}
