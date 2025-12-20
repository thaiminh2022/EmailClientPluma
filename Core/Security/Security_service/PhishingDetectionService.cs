using System.Net.Http;
using System.Net.Http.Json;
using EmailClientPluma.Security.Models;

namespace EmailClientPluma.Security.Services
{
    public class PhishingDetectionService
    {
        private readonly HttpClient _httpClient;

        public PhishingDetectionService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<PhishingResult> DetectAsync(string emailContent)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "http://127.0.0.1:8000/detect",
                new { content = emailContent }
            );

            return await response.Content.ReadFromJsonAsync<PhishingResult>();
        }
    }
}
