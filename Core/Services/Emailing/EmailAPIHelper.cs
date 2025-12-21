using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Storaging;

namespace EmailClientPluma.Core.Services.Emailing;

internal static class EmailAPIHelper
{
    public static async Task<string?> GetLastSyncedHistoryIdAsync(Account acc, IStorageService storageService)
    {
        var emails = await storageService.GetEmailsAsync(acc);
        return emails
            .Where(e => !string.IsNullOrEmpty(e.MessageIdentifiers.ProviderHistoryId))
            .OrderByDescending(e => ulong.Parse(e.MessageIdentifiers.ProviderHistoryId ?? "0"))
            .Select(e => e.MessageIdentifiers.ProviderHistoryId)
            .FirstOrDefault();
    }

    public static async Task<string?> GetOldestSyncedMessageIdAsync(Account acc, IStorageService storageService)
    {
        var emails = await storageService.GetEmailsAsync(acc);
        return emails
            .OrderBy(e => e.MessageParts.Date)
            .Select(e => e.MessageIdentifiers.ProviderMessageId)
            .FirstOrDefault();
    }
}