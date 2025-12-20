using EmailClientPluma.Core.Models;

namespace EmailClientPluma.Core.Services
{
    public interface IPhishingDetectionService
    {
        Task<PhishingResult> CheckAsync(string subject, string body);
    }
}
