using EmailClientPluma.Core.Models;
using Microsoft.Data.Sqlite;
using Dapper;
using MailKit;

namespace EmailClientPluma.Core.Services.Storaging
{
    internal class EmailStorage
    {
        private readonly string _connectionString;

        private SqliteConnection CreateConnection() => new(_connectionString);

        public EmailStorage(string connectionString)
        {
            _connectionString = connectionString;
        }
        public async Task StoreEmailsInternal(Account acc, List<Email> mails)
        {

            const string sql = @"
                INSERT INTO EMAILS (
                    IMAP_UID,
                    IMAP_UID_VALIDITY,
                    FOLDER_FULLNAME,
                    MESSAGE_ID,
                    OWNER_ID,
                    IN_REPLY_TO,
                    SUBJECT,
                    BODY,
                    FROM_ADDRESS,
                    TO_ADDRESS,
                    DATE,
                    FLAGS
                ) VALUES (
                    @ImapUid,
                    @ImapUidValidity,
                    @FolderFullName,
                    @MessageId,
                    @OwnerId,
                    @InReplyTo,
                    @Subject,
                    @Body,
                    @From,
                    @To,
                    @Date,
                    @Flags
                )
                ON CONFLICT (OWNER_ID, FOLDER_FULLNAME, IMAP_UID_VALIDITY, IMAP_UID)
                DO UPDATE SET
                    SUBJECT      = excluded.SUBJECT,
                    BODY         = COALESCE(excluded.BODY, EMAILS.BODY),
                    FROM_ADDRESS = excluded.FROM_ADDRESS,
                    TO_ADDRESS   = excluded.TO_ADDRESS,
                    DATE         = excluded.DATE
                RETURNING EMAIL_ID;
            ";
            
            
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            
            try
            {
                foreach (var m in mails)
                {
                    var msgPart = m.MessageParts;
                    var msgId = m.MessageIdentifiers;

                    var emailId = await connection.ExecuteScalarAsync<int>(
                        sql,
                        new
                        {
                            ImapUid = msgId.ImapUid,
                            ImapUidValidity = msgId.ImapUidValidity,
                            FolderFullName = msgId.FolderFullName,
                            MessageId = msgId.MessageId,
                            OwnerId = acc.ProviderUID,
                            InReplyTo = msgId.InReplyTo,
                            Subject = msgPart.Subject,
                            Body = msgPart.Body,
                            From = msgPart.From,
                            To = msgPart.To,
                            Date = msgPart.Date?.ToString("o"),
                            Flags = (int)msgId.Flags
                        });

                    m.MessageIdentifiers.EmailId = emailId;
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Error("Storing email exception: ", ex);
            }
        }
        
        public async Task<List<Email>> GetEmailsAsync(Account acc)
        {
            await using var connection = CreateConnection();
            var sql = @"
                        SELECT  EMAIL_ID,
                                IMAP_UID,
                                IMAP_UID_VALIDITY,
                                FOLDER_FULLNAME,
                                MESSAGE_ID,
                                OWNER_ID,
                                IN_REPLY_TO,
                                SUBJECT,
                                BODY,
                                FROM_ADDRESS,
                                TO_ADDRESS,
                                DATE,
                                FLAGS
                        FROM EMAILS
                        WHERE OWNER_ID = @OwnerId
                        --ORDER BY DATE DESC
                       ";
            var rows = await connection.QueryAsync<EmailRow>(sql, new { OwnerId = acc.ProviderUID });
            var emails = rows.Select(r =>
            {
                DateTimeOffset? date = null;
                if (!string.IsNullOrEmpty(r.DATE))
                {
                    date = DateTimeOffset.Parse(
                        r.DATE,
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind);
                }

                return new Email(
                    new Email.Identifiers
                    {
                        EmailId = r.EMAIL_ID,
                        ImapUid = r.IMAP_UID,
                        ImapUidValidity = r.IMAP_UID_VALIDITY,
                        FolderFullName = r.FOLDER_FULLNAME,
                        MessageId = r.MESSAGE_ID,
                        OwnerAccountId = r.OWNER_ID,
                        InReplyTo = r.IN_REPLY_TO,
                        Flags = (MessageFlags)r.FLAGS
                    },
                    new Email.DataParts
                    {
                        Subject = r.SUBJECT,
                        Body = r.BODY,
                        From = r.FROM_ADDRESS,
                        To = r.TO_ADDRESS,
                        Date = date
                    }
                );
            }).ToList();
            return emails;
        }
        public async Task UpdateEmailBodyAsync(Email email)
        {
            var sql = @"
                UPDATE EMAILS
                SET BODY = @Body
                WHERE EMAIL_ID = @EmailId
                   OR MESSAGE_ID = @MessageId;
                ";

            await using var connection = CreateConnection();

            await connection.ExecuteAsync(sql, new
            {
                EmailId = email.MessageIdentifiers.EmailId,
                MessageId = email.MessageIdentifiers.MessageId,
                Body = email.MessageParts.Body
            });
        }
    }
}
