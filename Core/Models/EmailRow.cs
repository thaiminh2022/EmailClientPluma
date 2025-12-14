namespace EmailClientPluma.Core.Models;

public sealed class EmailRow
{
    public int EMAIL_ID { get; set; }
    public string PROVIDER_MESSAGE_ID { get; init; }
    public string? PROVIDER_THREAD_ID { get; init; }
    public string? PROVIDER_HISTORY_ID { get; init; }
    public string? INTERNET_MESSAGE_ID { get; init; }
    public string FOLDER_FULLNAME { get; init; }
    public int PROVIDER { get; init; }
    public string OWNER_ID { get; init; }
    public string? IN_REPLY_TO { get; init; }
    public int FLAGS { get; init; }
    public uint IMAP_UID { get; init; }
    public uint IMAP_UID_VALIDITY { get; init; }


    public string SUBJECT { get; init; }
    public string? BODY { get; init; }
    public string FROM_ADDRESS { get; init; }
    public string TO_ADDRESS { get; init; }
    public string? DATE { get; init; }
}