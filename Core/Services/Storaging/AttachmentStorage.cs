

using Microsoft.Data.Sqlite;
using EmailClientPluma.Core.Models;


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

        foreach (var part in attachments)
        {
            if (part == null) continue;

            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
            INSERT OR IGNORE INTO ATTACHMENTS
            (EMAIL_ID, FILENAME, MIMETYPE, SIZE, CREATEDUTC,
             IMAP_UID, UID_VALIDITY, FOLDER_FULLNAME, MIME_PART_PATH)
            VALUES
            (@email,@name,@mime,@size,@utc,@uid,@valid,@folder,@path);
            """;

            cmd.Parameters.AddWithValue("@email", email.MessageIdentifiers.EmailId);
            cmd.Parameters.AddWithValue("@name", part.FileName);
            cmd.Parameters.AddWithValue("@mime", part.MimeType);
            cmd.Parameters.AddWithValue("@size", part.Size);
            cmd.Parameters.AddWithValue("@utc", DateTime.UtcNow);

            cmd.Parameters.AddWithValue("@uid", part.ImapUid);
            cmd.Parameters.AddWithValue("@valid", part.ImapUidValidity);
            cmd.Parameters.AddWithValue("@folder", part.FolderFullName);
            cmd.Parameters.AddWithValue("@path", part.MimePartPath);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<Attachment>> GetAttachmentsAsync(Email email)
    {
        var list = new List<Attachment>();

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM ATTACHMENTS WHERE EMAIL_ID = @id;";
        cmd.Parameters.AddWithValue("@id", email.MessageIdentifiers.EmailId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Attachment
            {
                AttachmentID = reader.GetInt32(reader.GetOrdinal("ATTACHMENT_ID")),
                OwnerEmailID = reader.GetInt32(reader.GetOrdinal("EMAIL_ID")),
                FileName = reader["FILENAME"]?.ToString(),
                MimeType = reader["MIMETYPE"]?.ToString(),
                Size = reader.GetInt64(reader.GetOrdinal("SIZE")),
                ImapUid = (uint)reader.GetInt64(reader.GetOrdinal("IMAP_UID")),
                ImapUidValidity = (uint)reader.GetInt64(reader.GetOrdinal("UID_VALIDITY")),
                FolderFullName = reader["FOLDER_FULLNAME"].ToString(),
                MimePartPath = reader["MIME_PART_PATH"].ToString()
            });
        }

        return list;
    }

    public async Task<bool> DeleteAttachmentInternal(Attachment attachment)
    {
        if (attachment == null) return false;

        using var connection = CreateConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ATTACHMENTS WHERE ATTACHMENT_ID = @id;";
        cmd.Parameters.AddWithValue("@id", attachment.AttachmentID);

        await cmd.ExecuteNonQueryAsync();
        return true;
    }
}
