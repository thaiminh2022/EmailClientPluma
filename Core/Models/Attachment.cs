namespace EmailClientPluma.Core.Models;

internal class Attachment
{
    public int Id { get; set; } = 0; // set by database
    public int OwnerEmailId { get; set; }

    public string? FileName { get; init; }
    public string? FilePath { get; set; }

    public bool ContentFetched => !string.IsNullOrWhiteSpace(FilePath);

    public string? ProviderAttachmentId { get; init; }

    // MIME info
    public string ContentType { get; init; } = "application/octet-stream";
    public long SizeBytes { get; init; }

    // helper
    public string SizeDisplayMb => $"{Math.Round(SizeBytes / 1_000_000f, 2)}MB";
}

internal record ATTACHMENT_ROW
{
    public int ID { get; init; }
    public int OWNER_EMAIL_ID { get; init; }
    public string? FILE_NAME { get; init; }
    public string? FILE_PATH { get; set; }
    public string? PROVIDER_ATTACHMENT_ID { get; init; }
    public string CONTENT_TYPE { get; init; }
    public long SIZE_BYTES { get; init; }
}

