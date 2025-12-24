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
}
