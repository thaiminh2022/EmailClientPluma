using EmailClientPluma.Core.Models;

namespace EmailClientPluma.Core.Services.Emailing;

internal class OutlookApiEmailService : IEmailService
{
    public Task FetchEmailHeaderAsync(Account acc)
    {
        throw new NotImplementedException();
    }

    public Task FetchEmailBodyAsync(Account acc, Email email)
    {
        throw new NotImplementedException();
    }

    public Task SendEmailAsync(Account acc, Email.OutgoingEmail email)
    {
        throw new NotImplementedException();
    }

    public Task PrefetchRecentBodiesAsync(Account acc, int maxToPrefetch = 30)
    {
        throw new NotImplementedException();
    }

    public Task<bool> FetchOlderHeadersAsync(Account acc, int window, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }
}