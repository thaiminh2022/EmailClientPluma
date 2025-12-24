using System.Net.Http;
using System.Net.NetworkInformation;

namespace EmailClientPluma.Core;
        
public static class InternetHelper
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };
    private static readonly Uri TestUri = new("https://www.msftconnecttest.com/connecttest.txt");

    static InternetHelper()
    {
    }
    
    // Might be wrong
    public static bool FastHasInternetConnection() => NetworkInterface.GetIsNetworkAvailable();    
    public static async Task<bool> HasInternetConnection()
    {
        var hasNetwork = NetworkInterface.GetIsNetworkAvailable();
        if (!hasNetwork) return false;
        
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, TestUri);
            using var res = await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            // technically "success" range
            return (int)res.StatusCode >= 200 && (int)res.StatusCode < 400;
        }
        catch
        {
            return false;
        }

    }
}