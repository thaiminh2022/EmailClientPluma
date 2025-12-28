using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EmailClientPluma.Core.Models;

namespace EmailClientPluma.Security.Services
{
    public class PhishingDetectionService
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public async Task<PhishingResult> CheckEmailAsync(string text)
        {
            var payload = new
            {
                text = text
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "http://127.0.0.1:8000/check",
                content
            );

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<PhishingResult>(responseJson);
        }
    }
}
