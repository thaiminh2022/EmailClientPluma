using EmailClientPluma.Core.Models;

namespace EmailClientPluma.Core.Services.Accounting
{
    /// <summary>
    /// Interface for all the authentication services (Google, Microsoft, Yahoo)
    /// </summary>
    interface IAuthenticationService
    {
        Provider GetProvider();

        Task<AuthResponce?> AuthenticateAsync();
        Task<bool> ValidateAsync(Account acc);
    }
}
