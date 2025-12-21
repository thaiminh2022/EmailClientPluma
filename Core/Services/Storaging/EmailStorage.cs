using Dapper;
using EmailClientPluma.Core.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace EmailClientPluma.Core.Services.Storaging;

internal class EmailStorage
{
    private readonly string _connectionString;

    public EmailStorage(string connectionString)
    {
        _connectionString = connectionString;
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public async Task StoreEmailsInternal(Account acc, List<Email> mails)
    {
        const string sql = """
                           INSERT INTO EMAILS (
                               PROVIDER_MESSAGE_ID,
                               PROVIDER_THREAD_ID,
                               PROVIDER_HISTORY_ID,
                               INTERNET_MESSAGE_ID,
                               FOLDER_FULLNAME,
                               PROVIDER,
                               OWNER_ID,
                               IN_REPLY_TO,
                               FLAGS,
                               IMAP_UID,
                               IMAP_UID_VALIDITY,
                               SUBJECT,
                               BODY,
                               FROM_ADDRESS,
                               TO_ADDRESS,
                               DATE
                           ) VALUES (
                               @ProviderMessageId,
                               @ProviderThreadId,
                               @ProviderHistoryId,
                               @InternetMessageId,
                               @FolderFullName,
                               @Provider,
                               @OwnerId,
                               @InReplyTo,
                               @Flags,
                               @ImapUid,
                               @ImapUidValidity,
                               @Subject,
                               @Body,
                               @FromAddress,
                               @ToAddress,
                               @Date
                           )
                           ON CONFLICT (OWNER_ID, FOLDER_FULLNAME, PROVIDER_MESSAGE_ID)
                           DO UPDATE SET
                               SUBJECT      = excluded.SUBJECT,
                               BODY         = COALESCE(excluded.BODY, EMAILS.BODY),
                               FROM_ADDRESS = excluded.FROM_ADDRESS,
                               TO_ADDRESS   = excluded.TO_ADDRESS,
                               DATE         = excluded.DATE
                           RETURNING EMAIL_ID;
                           """;


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
                        msgId.ProviderMessageId,
                        msgId.ProviderThreadId,
                        msgId.ProviderHistoryId,
                        msgId.InternetMessageId,
                        msgId.FolderFullName,
                        Provider = msgId.Provider.ToString(),
                        OwnerId = msgId.OwnerAccountId,
                        msgId.InReplyTo,
                        msgId.Flags,
                        msgId.ImapUid,
                        msgId.ImapUidValidity,
                        msgPart.Subject,
                        Body = msgPart.Body ?? "",
                        FromAddress = msgPart.From,
                        ToAddress = msgPart.To,
                        msgPart.Date
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
        var sql = """
                  SELECT  
                      EMAIL_ID,
                      PROVIDER_MESSAGE_ID,
                      PROVIDER_THREAD_ID,
                      PROVIDER_HISTORY_ID,
                      INTERNET_MESSAGE_ID,
                      FOLDER_FULLNAME,
                      PROVIDER,
                      OWNER_ID,
                      IN_REPLY_TO,
                      FLAGS,
                      IMAP_UID,
                      IMAP_UID_VALIDITY,
                      SUBJECT,
                      BODY,
                      FROM_ADDRESS,
                      TO_ADDRESS,
                      DATE
                      FROM EMAILS
                  WHERE OWNER_ID = @OwnerId
                  """;
        var rows = await connection.QueryAsync<EmailRow>(sql, new { OwnerId = acc.ProviderUID });
        var emails = rows.Select(r =>
        {
            DateTimeOffset? date = null;
            if (!string.IsNullOrEmpty(r.DATE))
                date = DateTimeOffset.Parse(
                    r.DATE,
                    null,
                    DateTimeStyles.RoundtripKind);

            return new Email(
                new Email.Identifiers
                {
                    EmailId = r.EMAIL_ID,
                    ProviderMessageId = r.PROVIDER_MESSAGE_ID,
                    ProviderThreadId = r.PROVIDER_THREAD_ID,
                    ProviderHistoryId = r.PROVIDER_HISTORY_ID,
                    InternetMessageId = r.INTERNET_MESSAGE_ID,
                    FolderFullName = r.FOLDER_FULLNAME,
                    Provider = Enum.Parse<Provider>(r.PROVIDER),
                    OwnerAccountId = r.OWNER_ID,
                    InReplyTo = r.IN_REPLY_TO,
                    Flags = (EmailFlags)r.FLAGS,
                    ImapUid = r.IMAP_UID,
                    ImapUidValidity = r.IMAP_UID_VALIDITY
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
        var sql = """
                  UPDATE EMAILS
                  SET BODY = @Body
                  WHERE EMAIL_ID = @EmailId
                  OR INTERNET_MESSAGE_ID = @MessageId;
                  """;

        await using var connection = CreateConnection();

        await connection.ExecuteAsync(sql, new
        {
            email.MessageIdentifiers.EmailId,
            MessageId = email.MessageIdentifiers.InternetMessageId,
            email.MessageParts.Body
        });
    }
}