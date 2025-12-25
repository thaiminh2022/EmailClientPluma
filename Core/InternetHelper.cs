using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Authentication;

namespace EmailClientPluma.Core;

public enum InternetStatus
{
    NoNetwork,
    Ok,
    CaptivePortalOrRedirect,
    TlsError,
    DnsError,
    Timeout,
    HttpError,
    UnknownError
}

public static class InternetHelper
{
    private static readonly Uri TestUri = new("http://www.msftconnecttest.com/connecttest.txt");
    private const string ExpectedBody = "Microsoft Connect Test";

    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false
    })
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    public static bool FastHasNetwork() => NetworkInterface.GetIsNetworkAvailable();

    public static async Task<(InternetStatus status, HttpStatusCode? code)> CheckAsync(
        CancellationToken ct = default)
    {
        if (!FastHasNetwork())
            return (InternetStatus.NoNetwork, null);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, TestUri);
            using var res = await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)res.StatusCode is >= 300 and < 400)
                return (InternetStatus.CaptivePortalOrRedirect, res.StatusCode);

            if (!res.IsSuccessStatusCode)
                return (InternetStatus.HttpError, res.StatusCode);

            var body = await res.Content.ReadAsStringAsync(ct);
            
            if (!body.Contains(ExpectedBody, StringComparison.OrdinalIgnoreCase))
                return (InternetStatus.CaptivePortalOrRedirect, res.StatusCode);

            return (InternetStatus.Ok, res.StatusCode);
        }
        catch (TaskCanceledException) 
        {
            return (InternetStatus.Timeout, null);
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            return (InternetStatus.TlsError, null);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException
                                              {
                                                  SocketErrorCode: System.Net.Sockets.SocketError.HostNotFound
                                              })
        {
            return (InternetStatus.DnsError, null);
        }
        catch
        {
            return (InternetStatus.UnknownError, null);
        }
    }

    public static async Task<bool> HasInternetConnection(CancellationToken ct = default)
        => (await CheckAsync(ct)).status is InternetStatus.Ok or InternetStatus.CaptivePortalOrRedirect;
}
