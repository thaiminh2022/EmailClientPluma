namespace EmailClientPluma.Core.Models
{
    public sealed class AttachmentRow
    {
        public int ATTACHMENT_ID { get; set; }
        public int EMAIL_ID { get; set; }

        public string FILENAME { get; set; } = "";
        public string MIMETYPE { get; set; } = "";
        public long SIZE { get; set; }

        public long CREATEDUTC { get; set; }
        

    }
}


    //CREATE TABLE IF NOT EXISTS ATTACHMENTS(
    //                        ATTACHMENT_ID INTEGER PRIMARY KEY AUTOINCREMENT,
    //                        EMAIL_ID INTEGER NOT NULL,
    //                        FILENAME TEXT    NOT NULL,
    //                        MIMETYPE TEXT    NOT NULL,
    //                        SIZE INTEGER NOT NULL,
    //                        STORAGE_KEY TEXT    NOT NULL,
    //                        CREATEDUTC TEXT    NOT NULL,
    //                        FOREIGN KEY (EMAIL_ID) REFERENCES EMAILS(EMAIL_ID) ON DELETE CASCADE
    //                    );";