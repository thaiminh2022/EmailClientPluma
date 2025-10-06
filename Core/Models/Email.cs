using EmailClientPluma.Core;
using System;
using System.Linq;

namespace EmailClientPluma.Core.Models
{
    internal class Email : ObserableObject
    {
        public int EmailID { get; set; } // set by database
        public string OwnerAccountID { get; set; }

        string _subject = string.Empty;
        public string Subject
        {
            get => _subject;
            set
            {
                if (_subject == value) return;
                _subject = value;
                OnPropertyChanges();
            }
        }

        string? _body;
        public string? Body
        {
            get => _body;
            set
            {
                if (_body == value) return;
                _body = value;
                OnPropertyChanges();
                OnPropertyChanges(nameof(IsBodyLoaded));
            }
        }

        string _from = string.Empty;
        public string From
        {
            get => _from;
            set
            {
                if (_from == value) return;
                _from = value;
                OnPropertyChanges();
            }
        }

        string[] _to = [];
        public string[] To
        {
            get => _to;
            set
            {
                if (_to == value) return;
                _to = value;
                OnPropertyChanges();
            }
        }

        public uint? ImapUid { get; set; }

        public bool IsBodyLoaded => !string.IsNullOrEmpty(Body);

        public IEnumerable<Attachment> Attachments { get; set; }

        public Email(string ownerAccountID, string subject, string? body, string from, string to, IEnumerable<Attachment> attachments, uint? imapUid = null)
        {
            OwnerAccountID = ownerAccountID;
            Subject = subject;
            Body = body;
            From = from;
            To = string.IsNullOrWhiteSpace(to)
                ? []
                : to.Split(',').Select(address => address.Trim()).Where(address => !string.IsNullOrEmpty(address)).ToArray();
            Attachments = attachments;
            ImapUid = imapUid;
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
