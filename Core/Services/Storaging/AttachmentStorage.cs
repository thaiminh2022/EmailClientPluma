using System.IO;
using Dapper;
using EmailClientPluma.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace EmailClientPluma.Core.Services.Storaging
{
    internal class AttachmentStorage(string connectionString, ILogger<StorageService> logger)
    {
        private SqliteConnection CreateConnection()
        {
            return new SqliteConnection(connectionString);
        }

        public async Task<IEnumerable<Attachment>> GetAttachmentAsync(Email mail)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            
            var sql = """
                      SELECT ID, OWNER_EMAIL_ID, FILE_NAME, FILE_PATH, PROVIDER_ATTACHMENT_ID, CONTENT_TYPE, SIZE_BYTES
                      FROM ATTACHMENTS
                      WHERE OWNER_EMAIL_ID = @OwnerEmailId
                      """;

            var results = await conn.QueryAsync<ATTACHMENT_ROW>(sql, new
            {
                OwnerEmailId = mail.MessageIdentifiers.EmailId
            });


            return results.Select(x => new Attachment
            {
                Id = x.ID,
                OwnerEmailId = x.OWNER_EMAIL_ID,
                FileName = x.FILE_NAME,
                FilePath = File.Exists(x.FILE_PATH) ? x.FILE_PATH : null,
                ProviderAttachmentId = x.PROVIDER_ATTACHMENT_ID,
                ContentType = x.CONTENT_TYPE,
                SizeBytes = x.SIZE_BYTES
            });
        }

        public async Task StoreAttachmentRefAsync(Email mail)
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();


            var tx = await conn.BeginTransactionAsync();
            var sql = """
                      INSERT INTO ATTACHMENTS (OWNER_EMAIL_ID, FILE_NAME, FILE_PATH, PROVIDER_ATTACHMENT_ID, CONTENT_TYPE, SIZE_BYTES)
                      VALUES (@OwnerEmailid, @FileName, @FilePath, @ProviderAttachmentId, @ContentType, @SizeBytes)
                      RETURNING ID
                      """;

            foreach (var attachment in mail.MessageParts.Attachments)
            {
                var id = conn.ExecuteScalar<int>(sql, new
                {
                    OwnerEmailId = mail.MessageIdentifiers.EmailId,
                    FileName = attachment.FileName,
                    FilePath = attachment.FilePath,
                    ProviderAttachmentId = attachment.ProviderAttachmentId,
                    ContentType = attachment.ContentType,
                    SizeBytes = attachment.SizeBytes
                }, transaction: tx);

                attachment.Id = id;
                attachment.OwnerEmailId =mail.MessageIdentifiers.EmailId;
            }
            await tx.CommitAsync();
        }


        private string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "ATTACHMENT" : name;
        }

        public async Task<string> UpdateAttachmentContentAsync(Email mail, Attachment attachment, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(attachment.FileName))
            {
                throw new InvalidOperationException("path is null");
            }

            var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(attachment.FileName));
            var ext = Path.GetExtension(attachment.FileName);
            var i = 1;

            var dest = Path.Combine(Helper.AttachmentsFolder, attachment.FileName);
            while (File.Exists(dest))
            {
                dest = Path.Combine(Helper.AttachmentsFolder, $"{baseName} ({i++}){ext}");
            }

            await File.WriteAllBytesAsync(dest, data);

            await using var conn = CreateConnection();
            var tx = await conn.BeginTransactionAsync();
            var sql = """
                      UPDATE ATTACHMENTS SET FILE_PATH = @FilePath
                      WHERE ID = @Id AND OWNER_EMAIL_ID = @EmailOwnerId
                      """;

            await conn.ExecuteAsync(sql, new
            {
                Id = attachment.Id,
                EmailOwnerId = attachment.OwnerEmailId
            }, transaction: tx);
            await tx.CommitAsync();
            attachment.FilePath = dest;

            return dest;


        }
    }
}
