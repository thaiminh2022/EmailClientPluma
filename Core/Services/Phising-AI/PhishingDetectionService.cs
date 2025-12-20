using System.Net.Http;
using System.Net.Http.Json;
using EmailClientPluma.Core.Models;

namespace EmailClientPluma.Core.Services
{
    public class PhishingDetectionService : IPhishingDetectionService
    {
        private readonly HttpClient _httpClient;

        public PhishingDetectionService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:8000")
            };
        }

        public async Task<PhishingResult> CheckAsync(string subject, string body)
        {
            var payload = new
            {
                text = $"{subject}\n{body}"
            };

            var response = await _httpClient.PostAsJsonAsync("/check", payload);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PhishingResult>();

            return result ?? new PhishingResult
            {
                Is_Phishing = false,
                Score = 0
            };
        }
    }
}
  