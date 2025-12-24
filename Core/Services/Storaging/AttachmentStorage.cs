using Dapper;
using EmailClientPluma.Core.Models;
using MailKit;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Security.Cryptography;

namespace EmailClientPluma.Core.Services.Storaging
{
    internal class AttachmentStorage
    {
        private readonly string _connectionString;

        private SqliteConnection CreateConnection() => new(_connectionString);

        public AttachmentStorage(string connectionString)
        {
            _connectionString = connectionString;
        }
        public async Task StoreAttachmentsInternal(Email email, List<Attachment> attachments)
        {

            using var connection = CreateConnection();
            await connection.OpenAsync();


            foreach (var part in email.MessageParts.Attachments)
            {
                // 1. Decode bytes

                byte[] bytes = part.Content;

                // 2. Generate storage key
                string storageKey = Convert.ToHexString(
                    SHA256.HashData(bytes));


                // 3. Save to vault (dedup)
                DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(Helper.DataFolder, "Attachments"));
                string path = Path.Combine(directory.FullName, storageKey);

                if (!File.Exists(path))
                    await File.WriteAllBytesAsync(path, bytes);

                // 4. Insert metadata row
                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                """
                INSERT INTO ATTACHMENTS
                (EMAIL_ID, FILENAME, MIMETYPE, SIZE, STORAGE_KEY, CREATEDUTC)
                VALUES (@email,@name,@mime,@size,@key,@utc);
                """;

                cmd.Parameters.AddWithValue("@email", email.MessageIdentifiers.EmailId);
                cmd.Parameters.AddWithValue("@name", part.FileName);
                cmd.Parameters.AddWithValue("@mime", part.MimeType);
                cmd.Parameters.AddWithValue("@size", bytes.Length);
                cmd.Parameters.AddWithValue("@key", storageKey);
                cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow);

                await cmd.ExecuteNonQueryAsync();
            }
        }


        public async Task<List<Attachment>> GetAttachmentsAsync(Email email)
        {
            using var connection = CreateConnection();
            var sql = @"
                        SELECT ATTACHMENT_ID,
                               EMAIL_ID,
                               FILENAME,
                               MIMETYPE,
                               SIZE,
                               STORAGE_KEY,
                               CREATEDUTC
                        FROM ATTACHMENTS
                        WHERE EMAILID = @EmailId
                       ";
            var rows = await connection.QueryAsync<AttachmentRow>(sql, new { EmailId = email.MessageIdentifiers.EmailID });
            var attachments = rows.Select(r =>
            {
                return new Attachment
                {
                    AttachmentID = r.ATTACHMENT_ID,
                    OwnerEmailID = r.EMAIL_ID,
                    FileName = r.FILENAME,
                    MimeType = r.MIMETYPE,
                    Content = File.ReadAllBytes(
                        Path.Combine(Helper.DataFolder, r.STORAGE_KEY)),
                };
            }).ToList();
            return attachments;
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
