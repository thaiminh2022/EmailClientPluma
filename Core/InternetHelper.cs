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
    // msft captive portal detection endpoint used by Windows
    private static readonly Uri TestUri = new("http://www.msftconnecttest.com/connecttest.txt");

    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = false
    })
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    // Cache so 50 api call in 1 secs only result in 1
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20); // cache time is 20secs

    private static (InternetStatus status, HttpStatusCode? code) _cached = (InternetStatus.UnknownError, null);
    private static DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    private static bool _networkHooksInstalled;

    /// <summary>
    /// Call once at app startup (optional but recommended).
    /// </summary>
    public static void InstallNetworkChangeHooks()
    {
        // When network changes, get new cache

        if (_networkHooksInstalled) return;
        _networkHooksInstalled = true;

        NetworkChange.NetworkAvailabilityChanged += (_, __) => InvalidateCache();
        NetworkChange.NetworkAddressChanged += (_, __) => InvalidateCache();
    }

    public static void InvalidateCache() => _cachedAt = DateTimeOffset.MinValue;

    public static bool FastHasNetwork() => NetworkInterface.GetIsNetworkAvailable();


    public static async Task<(InternetStatus status, HttpStatusCode? code)> CheckAsync(
        bool force = false,
        CancellationToken ct = default)
    {
        // check cache if have internet
        var now = DateTimeOffset.UtcNow;
        if (!force && (now - _cachedAt) < CacheTtl)
            return _cached;

        // semaphore bullshits i learn from HDH
        await Gate.WaitAsync(ct);
        try
        {
            // if wait for too long, cache may got updated
            now = DateTimeOffset.UtcNow;
            if (!force && (now - _cachedAt) < CacheTtl)
                return _cached;

            var result = await CheckCoreAsync(ct);
            _cached = result;
            _cachedAt = DateTimeOffset.UtcNow;
            return result;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<(InternetStatus status, HttpStatusCode? code)> CheckCoreAsync(CancellationToken ct)
    {
        if (!FastHasNetwork())
            return (InternetStatus.NoNetwork, null);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, TestUri);
            using var res = await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            // 3xx without following redirects is a strong captive portal / login page signal
            if ((int)res.StatusCode is >= 300 and < 400)
                return (InternetStatus.CaptivePortalOrRedirect, res.StatusCode);

            if (!res.IsSuccessStatusCode)
                return (InternetStatus.HttpError, res.StatusCode);

            return (InternetStatus.Ok, res.StatusCode);
        }
        catch (TaskCanceledException)
        {
            // includes HttpClient.Timeout
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

    public static async Task<bool> HasInternetConnection(bool force = false, CancellationToken ct = default)
    {
        var result = await CheckAsync(force, ct);
        return result.status is InternetStatus.Ok or InternetStatus.CaptivePortalOrRedirect;
    }


}
