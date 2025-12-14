using MailKit;
using System.Collections.ObjectModel;
using System.Globalization;
using EmailClientPluma.Core.Services.Accounting;

namespace EmailClientPluma.Core.Models
{
    
    // Universal email flags that work across all providers
    [Flags]
    internal enum EmailFlags
    {
        None = 0,
        Seen = 1 << 0,      
        Flagged = 1 << 2,     
        Deleted = 1 << 3,    
        Draft = 1 << 4,       
        Spam = 1 << 5,
        Sent = 1 << 6,
        Important = 1 << 7,
    }

    internal class Email : ObserableObject
    {
        public record Identifiers
        {
            public int EmailId { get; set; } // set by database
            public required string ProviderMessageId { get; set; }
            public string? ProviderThreadId { get; set; }
            public string? ProviderHistoryId { get; set; }
            public string? InternetMessageId { get; set; }
            
            public required string FolderFullName { get; set; }
            
            public required Provider Provider { get; set; }
            
            public required string OwnerAccountId { get; set; }
            public string? InReplyTo { get; set; }
            
            public required EmailFlags Flags { get; set; }
            
            public uint? ImapUid { get; set; }
            public uint? ImapUidValidity { get; set; }
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

        }

        internal class OutgoingEmail : DataParts
        {
            public required string? InReplyTo { get; set; }
            public required string? ReplyTo { get; set; }
        }

        public Identifiers MessageIdentifiers { get; set; }
        public DataParts MessageParts { get; set; }

        public ObservableCollection<EmailLabel> Labels { get; set; }

        public bool BodyFetched => !string.IsNullOrEmpty(MessageParts.Body);
        public Email(Identifiers messageIdentifiers, DataParts messagesParts)
        {
            MessageIdentifiers = messageIdentifiers;
            MessageParts = messagesParts;
            Labels = [];
        }
    }

    // Helper extension methods for working with different providers
    internal static class EmailIdentifierExtensions
    {
        public static bool IsSameMessage(this Email.Identifiers id1, Email.Identifiers id2)
        {
            // First try InternetMessageId (most reliable across providers)
            if (!string.IsNullOrEmpty(id1.InternetMessageId) && 
                !string.IsNullOrEmpty(id2.InternetMessageId))
            {
                return string.Equals(id1.InternetMessageId, id2.InternetMessageId, 
                    StringComparison.OrdinalIgnoreCase);
            }

            // Fallback to provider-specific ID
            if (id1.Provider == id2.Provider)
            {
                return string.Equals(id1.ProviderMessageId, id2.ProviderMessageId, 
                    StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        public static EmailFlags FromGmailLabels(IList<string> labelIds)
        {
            var flags = EmailFlags.None;

            if (!labelIds.Contains("UNREAD"))
                flags |= EmailFlags.Seen;

            if (labelIds.Contains("STARRED"))
                flags |= EmailFlags.Flagged;

            if (labelIds.Contains("DRAFT"))
                flags |= EmailFlags.Draft;

            if (labelIds.Contains("TRASH"))
                flags |= EmailFlags.Deleted;
            
            if (labelIds.Contains("SPAM"))
                flags |= EmailFlags.Spam;
            
            if (labelIds.Contains("SENT"))
                flags |= EmailFlags.Sent;
            
            if (labelIds.Contains("IMPORTANT"))
                flags |= EmailFlags.Important;

            return flags;
        }
        
        public static bool IsInSameThread(this Email.Identifiers id1, Email.Identifiers id2)
        {
            // Provider-specific thread ID
            if (!string.IsNullOrEmpty(id1.ProviderThreadId) && 
                !string.IsNullOrEmpty(id2.ProviderThreadId))
            {
                return string.Equals(id1.ProviderThreadId, id2.ProviderThreadId, 
                    StringComparison.OrdinalIgnoreCase);
            }

            // Fallback to In-Reply-To matching
            if (!string.IsNullOrEmpty(id1.InReplyTo) && 
                !string.IsNullOrEmpty(id2.InternetMessageId))
            {
                return string.Equals(id1.InReplyTo, id2.InternetMessageId, 
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(id2.InReplyTo) && 
                !string.IsNullOrEmpty(id1.InternetMessageId))
            {
                return string.Equals(id2.InReplyTo, id1.InternetMessageId, 
                    StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}