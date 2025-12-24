using Dapper;
using EmailClientPluma.Core.Models;
using MailKit;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
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
        public async Task StoreAttachmentsInternal(Email email, IEnumerable<Attachment> attachments)
        {

            using var connection = CreateConnection();
            await connection.OpenAsync();


            foreach (var part in email.MessageParts.Attachments)
            {

                //Insert metadata row
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
                cmd.Parameters.AddWithValue("@size", part.Size);
                cmd.Parameters.AddWithValue("@key", part.StorageKey);
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
            var rows = await connection.QueryAsync<AttachmentRow>(sql, new { EmailId = email.MessageIdentifiers.EmailId });
            var attachments = rows.Select(r =>
            {
                return new Attachment
                {
                    AttachmentID = r.ATTACHMENT_ID,
                    OwnerEmailID = r.EMAIL_ID,
                    FileName = r.FILENAME,
                    MimeType = r.MIMETYPE
                };
            }).ToList();
            return attachments;
        }

        public async Task<bool> DeleteAttachmentInternal(Attachment attachment)
        {
           
            if (attachment == null) return false;

            // Delete file from disk
            if (File.Exists(attachment.FilePath))
            {
                File.Delete(attachment.FilePath);
            }

            // Remove from DB
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
                DELETE FROM ATTACHMENTS
                WHERE StorageKey = @storageKey;
            """;
            cmd.Parameters.AddWithValue("@storageKey", attachment.StorageKey);

            await cmd.ExecuteNonQueryAsync();

            //delete itself
            attachment = null;

            return true;

            
        }

        
    }
}

                