using EmailClientPluma.Core.Models;

namespace EmailClientPluma.Core.Services.Accounting;

internal class MicrosoftAuthenticationService : IAuthenticationService
{

    public Provider GetProvider()
    {
        return Provider.Microsoft;
    }

    public async Task<AuthResponce?> AuthenticateAsync()
    {
        
    }

    public async Task<bool> ValidateAsync(Account acc)
    {
        return true;
    }
}