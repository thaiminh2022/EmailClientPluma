using EmailClientPluma.Core.Models;

namespace EmailClientPluma.Core.Services.Emailing;

interface IEmailService
{
    Task FetchEmailHeaderAsync(Account acc);
    Task FetchEmailBodyAsync(Account acc, Email email);
    Task SendEmailAsync(Account acc, Email.OutgoingEmail email);
    Task PrefetchRecentBodiesAsync(Account acc, int maxToPrefetch = 30);
    Task<bool> FetchOlderHeadersAsync(Account acc, int window, CancellationToken token = default);
}