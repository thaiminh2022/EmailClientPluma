namespace EmailClientPluma.Core.Services
{
    internal partial class StorageService
    {
        private sealed class EmailRow
        {
            public int EMAIL_ID { get; set; }
            public uint IMAP_UID { get; set; }
            public uint IMAP_UID_VALIDITY { get; set; }
            public string FOLDER_FULLNAME { get; set; } = "";
            public string? MESSAGE_ID { get; set; }
            public string OWNER_ID { get; set; } = "";
            public string? IN_REPLY_TO { get; set; }

            public string SUBJECT { get; set; } = "";
            public string? BODY { get; set; }
            public string FROM_ADDRESS { get; set; } = "";
            public string TO_ADDRESS { get; set; } = "";
            public string? DATE { get; set; }
        }
    }

    internal partial class StorageService
    {
        private sealed class AttachmentRow
        {
            public long ATTACHMENT_ID { get; set; }
            public long EMAIL_ID { get; set; }

            public string FILENAME { get; set; } = null!;
            public string MIMETYPE { get; set; } = null!;
            public long SIZE { get; set; }

            public string STORAGE_KEY { get; set; } = null!;
            public string CREATEDUTC { get; set; } = null!;

        }
    }
}
