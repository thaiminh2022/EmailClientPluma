namespace EmailClientPluma.Core.Models;

public sealed class AccountRow
{
    public string PROVIDER_UID { get; init; }
    public string PROVIDER { get; init; }
    public string EMAIL { get; init; }
    public string DISPLAY_NAME { get; init; }
    public string? PAGINATION_TOKEN { get; init; }
    public string? LAST_SYNC_TOKEN { get; init; }
}