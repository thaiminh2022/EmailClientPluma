using EmailClientPluma.Core.Services;

namespace EmailClientPluma.Core.Models
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
