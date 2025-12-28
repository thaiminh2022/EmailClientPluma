using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace EmailClientPluma.Core.Services;

using HrefDisplay = (string href, string display);

public static partial class PhishDetector
{
    public enum SuspiciousLevel { None, Minor, Medium, Major }

    private static readonly HashSet<string> RedirectHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        // URL shorteners
        "bit.ly", "t.co", "ow.ly", "tinyurl.com", "is.gd", "amzn.to", "a.co", "lnkd.in", "aka.ms", "msft.it",

        // marketing / tracking commonly used
        "sendgrid.net", "mailchimp.com", "constantcontact.com",
        "mandrillapp.com", "hubspot.com", "mailgun.net", "postmarkapp.com", "sparkpostmail.com",
        "track.customer.io", "clicks.aweber.com", "url.emaildelivery.com",

        // Social redirectors
        "l.facebook.com", "l.instagram.com",

        // Reddit / YouTube short domains (often legit redirect/short)
        "redd.it", "youtu.be",

        // GitHub mail infra domains (can contain tracking/unsub)
        "notifications.github.com", "github-email.github.com",
    };

    private static readonly Dictionary<string, string[]> RedirectHostPathPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["google.com"] = ["/url", "/amp"],
        ["youtube.com"] = ["/redirect"],
        ["twitter.com"] = ["/i/redirect"],
        ["x.com"] = ["/i/redirect"],
        ["amazon.com"] = ["/gp/r.html"],
        ["reddit.com"] = ["/link"],
        ["office.com"] = ["/r"],
        ["quora.com"] = ["/q/", "/share"],
    };

    private static readonly HashSet<string> AllowedDisplayMismatch = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com", "quora.com", "reddit.com", "facebook.com", "linkedin.com",
        "twitter.com", "x.com", "youtube.com", "google.com", "amazon.com",
        "microsoft.com", "apple.com"
    };

    private static readonly HashSet<string> SuspiciousTlds = new(StringComparer.OrdinalIgnoreCase)
    {
        "tk", "ml", "ga", "cf", "gq",
        "xyz", "top", "work", "click", "link",
        "pw", "cc", "ws", "info", "biz"
    };

    private static readonly string[] PhishingKeywords =
    [
        "verify", "secure", "account", "update", "confirm",
        "login", "signin", "banking", "suspended", "locked",
        "urgent", "alert", "security", "password"
    ];

    // Common redirect query parameters
    private static readonly string[] RedirectParamNames =
    {
        "url", "u", "target", "dest", "destination", "redirect", "redir", "r", "continue", "next", "to"
    };

    // Very small set of known multi-label public suffixes you’ll encounter a lot.
    // (Not a full PSL; just a pragmatic improvement.)
    private static readonly HashSet<string> CommonPublicSuffix2 = new(StringComparer.OrdinalIgnoreCase)
    {
        "co.uk", "org.uk", "gov.uk", "ac.uk",
        "com.au", "net.au", "org.au",
        "co.jp",
        "com.vn", "net.vn", "org.vn", "gov.vn", "edu.vn"
    };

    // Multi-tenant “suffix-like” domains where registrable domain is often 3 labels
    private static readonly HashSet<string> MultiTenantSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.io", "pages.dev", "vercel.app", "netlify.app",
        "azurewebsites.net", "cloudfront.net"
    };

    public static List<HrefDisplay> ExtractUrlsFromHtml(string? html)
    {
        var list = new List<HrefDisplay>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var doc = new HtmlDocument();
        doc.LoadHtml(html ?? string.Empty);

        // 1) <a href="...">display</a>
        foreach (var node in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = node.GetAttributeValue("href", "").Trim();
            var text = WebUtility.HtmlDecode(node?.InnerText ?? "").Trim();

            if (!string.IsNullOrEmpty(href) && IsHttpUrl(href) && seen.Add(href))
                list.Add((href, text));
        }

        // 2) Plain text URLs
        var textContent = doc.DocumentNode.InnerText ?? string.Empty;
        foreach (Match m in PlainTextRegex().Matches(textContent))
        {
            var u = m.Value;
            if (IsHttpUrl(u) && seen.Add(u))
                list.Add((u, u));
        }

        return list;
    }

    public static (SuspiciousLevel, PhishingAnalysisResult) ValidateHtmlContent(string html, string sender)
    {
        var links = ExtractUrlsFromHtml(html);

        List<string> trustedDomains =
        [
            // Email providers
            "googleusercontent.com", "outlook.com", "hotmail.com", "live.com", "office.com",
            "microsoft.com", "icloud.com", "me.com", "yahoo.com", "google.com",
            "yahoo.co.jp", "proton.me", "protonmail.com", "gmail.com",

            // Identity
            "accounts.google.com", "login.microsoftonline.com",
            "appleid.apple.com", "auth0.com", "okta.com",

            // Banks & payment
            "paypal.com", "stripe.com", "visa.com", "mastercard.com", "americanexpress.com",

            // Dev
            "github.com", "githubusercontent.com", "gitlab.com", "bitbucket.org",
            "stackexchange.com", "stackoverflow.com", "cloudflare.com", "vercel.com",

            // VN local
            "zalo.me", "vnpt.vn", "vnpt.com.vn", "viettel.com.vn",
            "tiki.vn", "shopee.vn", "lazada.vn"
        ];

        var result = AnalyzeEmail(html, sender, trustedDomains);

        
        return (result.Score switch
        {
            0 => SuspiciousLevel.None,
            < 40 => SuspiciousLevel.Minor,
            < 80 => SuspiciousLevel.Medium,
            _ => SuspiciousLevel.Major
        }, result);
    }

    /// <summary>
    /// Detailed analysis for showing to users.
    /// NOTE: This analyzes both direct links and redirect destinations.
    /// </summary>
    private static PhishingAnalysisResult AnalyzeEmail(string html, string senderEmail, List<string> trustedDomains)
    {
        var links = ExtractUrlsFromHtml(html);
        var issues = new List<string>();

        // Score once to keep consistent with UI level
        var trustedDomainsList = trustedDomains.ToList();
        var score = ScoreLinks(links, trustedDomainsList);

        // Generate issues (best effort)
        foreach (var link in links)
        {
            if (!TryParseHttpUri(link.href, out var uri))
                continue;

            var host = NormalizeHost(uri.Host);
            if (host is null) continue;

            // If got redirected, try to analyze destination
            if (IsRedirectUri(uri))
            {
                var dest = ExtractRedirectTarget(uri);
                if (dest != null)
                {
                    var destHost = NormalizeHost(dest.Host);
                    if (destHost != null && !IsWhitelistedDomain(destHost, trustedDomainsList))
                    {
                        issues.Add($"Đường dẫn bị chuyển: {host} đến {destHost}");
                        AddHostIssues(issues, link, dest, destHost);
                    }
                }
                else
                {
                    issues.Add($"Đường dẫn bị chuyển đến địa chỉ không xác định: {host}");
                }

                continue;
            }

            // Non-redirect: direct host issues
            if (IsWhitelistedDomain(host, trustedDomainsList))
                continue;

            AddHostIssues(issues, link, uri, host);
        }

        return new PhishingAnalysisResult
        {
            Score = score,
            Level = score switch
            {
                0 => SuspiciousLevel.None,
                < 40 => SuspiciousLevel.Minor,
                < 80 => SuspiciousLevel.Medium,
                _ => SuspiciousLevel.Major
            },
            Issues = issues,
            TotalLinks = links.Count
        };
    }

    public static int ScoreLinks(IEnumerable<HrefDisplay> links, List<string> trustedDomains)
    {
        var total = 0;
        var redirectCount = 0;
        var suspiciousCount = 0;

        var trustedBaseDomains = trustedDomains
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => GetRegistrableDomain(d.Trim().ToLowerInvariant()))
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var link in links)
        {
            if (!TryParseHttpUri(link.href, out var uri))
                continue;

            var host = NormalizeHost(uri.Host);
            if (host is null) continue;

            // Redirect? score destination if possible
            if (IsRedirectUri(uri))
            {
                redirectCount++;

                var dest = ExtractRedirectTarget(uri);
                if (dest != null)
                {
                    var destHost = NormalizeHost(dest.Host);
                    if (destHost != null)
                    {
                        var s = ScoreHostAndDisplay(dest, link.display, destHost, trustedBaseDomains);
                        if (s > 0) suspiciousCount++;
                        total += s;
                    }
                }

                // small base penalty for redirects (optional)
                // total += 1;
                continue;
            }

            // Direct score
            if (IsWhitelistedDomain(host, trustedDomains))
                continue;

            var score = ScoreHostAndDisplay(uri, link.display, host, trustedBaseDomains);
            if (score > 0) suspiciousCount++;
            total += score;
        }

        // Aggregate tuning
        if (redirectCount > 20) total += 30;
        if (suspiciousCount > 5) total += 20;

        // Cap to avoid newsletters exploding
        if (total > 200) total = 200;

        return total;
    }

    // ----------------- core scoring helpers -----------------

    private static int ScoreHostAndDisplay(
        Uri uri,
        string display,
        string host,
        List<string> trustedBaseDomains)
    {
        int score = 0;

        var registrable = GetRegistrableDomain(host);

        // Punycode / IDN
        if (uri.Host.Contains("xn--", StringComparison.OrdinalIgnoreCase) ||
            uri.AbsoluteUri.Contains("xn--", StringComparison.OrdinalIgnoreCase))
            score += 30;

        // Raw IP host
        if (IsIpHost(host))
            score += 20;

        // Display mismatch only if display *looks like* a URL/domain
        if (DisplayLooksLikeUrl(display) && DisplayHrefMismatch(uri, display))
            score += 25;

        // Suspicious TLD
        var tld = GetTld(host);
        if (SuspiciousTlds.Contains(tld))
            score += 15;

        // Excessive subdomains (based on registrable domain label count)
        var subCount = GetSubdomainCount(host);
        if (subCount > 3)
            score += 10;

        // Phishing keywords in host
        var lowerHost = host.ToLowerInvariant();
        if (PhishingKeywords.Any(k => lowerHost.Contains(k)))
            score += 20;

        // Typosquatting vs trusted base domains
        if (!string.IsNullOrEmpty(registrable) && trustedBaseDomains.Count > 0)
        {
            int best = int.MaxValue;
            string? bestTrusted = null;

            foreach (var t in trustedBaseDomains)
            {
                var dist = Levenshtein(registrable, t);
                if (dist < best)
                {
                    best = dist;
                    bestTrusted = t;
                }
            }

            if (bestTrusted != null && best is > 0 and <= 2)
            {
                // If host is a subdomain of the trusted domain, it's not typosquatting.
                if (!host.EndsWith("." + bestTrusted, StringComparison.OrdinalIgnoreCase))
                    score += 40;
            }
        }

        // Cap per-link to avoid crazy totals
        if (score > 80) score = 80;

        return score;
    }

    private static void AddHostIssues(List<string> issues, HrefDisplay link, Uri uri, string host)
    {
        if (uri.AbsoluteUri.Contains("xn--", StringComparison.OrdinalIgnoreCase) || uri.Host.Contains("xn--", StringComparison.OrdinalIgnoreCase))
            issues.Add($"Phát hiện Punycode/IDN: {host}");

        if (IsIpHost(host))
            issues.Add($"Link là địa chỉ ip: {host}");

        if (DisplayLooksLikeUrl(link.display) && DisplayHrefMismatch(uri, link.display))
            issues.Add($"Đường dẫn gốc với chữ hiển thị không trùng khớp: \"{link.display}\" → {host}");

        if (PhishingKeywords.Any(k => host.Contains(k, StringComparison.OrdinalIgnoreCase)))
            issues.Add($"Tồn tại những từ có ngữ có thể dùng để phishing {host}");

        var tld = GetTld(host);
        if (SuspiciousTlds.Contains(tld))
            issues.Add($"TLD không đảm bảo .{tld}: {host}");

        var sub = GetSubdomainCount(host);
        if (sub > 3)
            issues.Add($"Quá nhiều link phụ ({sub}): {host}");
    }

    // ----------------- redirect detection + extraction -----------------

    private static bool IsRedirectUri(Uri uri)
    {
        var host = NormalizeHost(uri.Host);
        if (host is null) return false;

        if (RedirectHosts.Contains(host))
            return true;

        var baseDom = GetRegistrableDomain(host);
        if (!string.IsNullOrEmpty(baseDom) && RedirectHosts.Contains(baseDom))
            return true;

        // host+path checks
        var baseHost = GetRegistrableDomain(host);
        if (string.IsNullOrEmpty(baseHost))
            baseHost = host;

        if (RedirectHostPathPrefixes.TryGetValue(baseHost, out var prefixes))
        {
            var path = uri.AbsolutePath ?? "";
            foreach (var p in prefixes)
            {
                if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static Uri? ExtractRedirectTarget(Uri redirectUri)
    {
        // Try query params
        var q = redirectUri.Query;
        if (!string.IsNullOrWhiteSpace(q))
        {
            foreach (var key in RedirectParamNames)
            {
                var value = TryGetQueryParam(redirectUri, key);
                if (string.IsNullOrWhiteSpace(value)) continue;

                value = WebUtility.UrlDecode(value).Trim();

                // Sometimes nested encoding: decode twice safely
                var twice = WebUtility.UrlDecode(value);
                if (!string.IsNullOrWhiteSpace(twice)) value = twice.Trim();

                // Some redirectors pass without scheme (e.g., www.example.com/path)
                if (Uri.TryCreate(value, UriKind.Absolute, out var abs) && IsHttp(abs))
                    return abs;

                if (Uri.TryCreate("https://" + value, UriKind.Absolute, out var abs2) && IsHttp(abs2))
                    return abs2;
            }
        }

        // Some shorteners put destination in fragment or path; you could expand later (requires network)
        return null;
    }

    private static string? TryGetQueryParam(Uri uri, string name)
    {
        // Minimal query parser (no dependency)
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query)) return null;

        // trim leading '?'
        var q = query[0] == '?' ? query[1..] : query;

        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            var k = WebUtility.UrlDecode(kv[0]);
            if (!string.Equals(k, name, StringComparison.OrdinalIgnoreCase))
                continue;

            var v = kv.Length > 1 ? kv[1] : "";
            return v;
        }

        return null;
    }

    // ----------------- display mismatch -----------------

    private static bool DisplayHrefMismatch(Uri hrefUri, string displayText)
    {
        var hrefHost = NormalizeHost(hrefUri.Host);
        var displayHost = GetHostFromText(displayText);

        if (string.IsNullOrEmpty(hrefHost) || string.IsNullOrEmpty(displayHost))
            return false;

        var hrefBase = GetRegistrableDomain(hrefHost);
        var displayBase = GetRegistrableDomain(displayHost);

        if (string.IsNullOrEmpty(hrefBase) || string.IsNullOrEmpty(displayBase))
            return false;

        // If href host is a redirect endpoint, skip mismatch scoring (it will be handled by destination scoring)
        if (IsRedirectUri(hrefUri))
            return false;

        // Allow mismatch for certain big brands (your choice)
        if (AllowedDisplayMismatch.Contains(hrefBase))
            return false;

        return !hrefBase.Equals(displayBase, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DisplayLooksLikeUrl(string display)
    {
        if (string.IsNullOrWhiteSpace(display)) return false;

        display = display.Trim();

        // If it's "Click here" or has spaces, usually not a URL display
        if (display.Contains(' ') && !display.Contains("http", StringComparison.OrdinalIgnoreCase))
            return false;

        // Common URL-ish markers
        return display.Contains('.') || display.StartsWith("http", StringComparison.OrdinalIgnoreCase) || display.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetHostFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Try explicit URLs first
        var mUrl = Regex.Match(text, @"https?://[^\s""'<>]+", RegexOptions.IgnoreCase);
        if (mUrl.Success && TryParseHttpUri(mUrl.Value, out var uri))
            return NormalizeHost(uri.Host);

        // Then domain-like patterns
        var m = Regex.Match(text, @"\b([a-z0-9](?:[a-z0-9\-]{0,61}[a-z0-9])?\.[a-z0-9\.\-]{2,})\b", RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var host = m.Groups[1].Value.Trim().TrimEnd('.');
        return NormalizeHost(host);
    }

    // ----------------- whitelist -----------------

    private static bool IsWhitelistedDomain(string host, IEnumerable<string> trustedDomains)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        var registrable = GetRegistrableDomain(host);

        foreach (var trusted in trustedDomains ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(trusted)) continue;

            var tHost = trusted.Trim().ToLowerInvariant();
            var tReg = GetRegistrableDomain(tHost);

            // Exact host match
            if (host.Equals(tHost, StringComparison.OrdinalIgnoreCase))
                return true;

            // Registrable-domain match
            if (!string.IsNullOrEmpty(registrable) && !string.IsNullOrEmpty(tReg) &&
                registrable.Equals(tReg, StringComparison.OrdinalIgnoreCase))
                return true;

            // Subdomain match (must actually be subdomain of trusted)
            if (host.EndsWith("." + tHost, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(registrable) && !string.IsNullOrEmpty(tReg) &&
                registrable.Equals(tReg, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // ----------------- domain parsing -----------------

    private static string GetTld(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return "";
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "" : parts[^1].ToLowerInvariant();
    }

    private static int GetSubdomainCount(string host)
    {
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2) return 0;

        var reg = GetRegistrableDomain(host);
        if (string.IsNullOrEmpty(reg)) return Math.Max(0, parts.Length - 2);

        var regParts = reg.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var sub = parts.Length - regParts.Length;
        return sub < 0 ? 0 : sub;
    }

    /// <summary>
    /// Registrable domain heuristic:
    /// - Handles common public suffixes (co.uk, com.vn, ...)
    /// - Handles multi-tenant suffixes (github.io, pages.dev, ...)
    /// Not a full PSL, but much better than "last two labels".
    /// </summary>
    private static string GetRegistrableDomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return host;

        host = NormalizeHost(host) ?? host.ToLowerInvariant();

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2) return host;

        // Multi-tenant suffix: keep last 3 labels (user.github.io)
        var last2 = string.Join('.', parts[^2], parts[^1]);
        if (MultiTenantSuffixes.Contains(last2) && parts.Length >= 3)
            return string.Join('.', parts[^3], parts[^2], parts[^1]).ToLowerInvariant();

        // Common 2-part public suffixes: keep last 3 labels (bbc.co.uk)
        if (parts.Length >= 3)
        {
            var lastTwo = string.Join('.', parts[^2], parts[^1]);
            if (CommonPublicSuffix2.Contains(lastTwo))
                return string.Join('.', parts[^3], parts[^2], parts[^1]).ToLowerInvariant();
        }

        // Default: last two
        return string.Join('.', parts[^2], parts[^1]).ToLowerInvariant();
    }

    private static string? NormalizeHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        host = host.Trim().TrimEnd('.').ToLowerInvariant();

        // Remove IPv6 brackets if present
        if (host.StartsWith("[") && host.EndsWith("]") && host.Length > 2)
            host = host[1..^1];

        return host;
    }

    private static bool IsIpHost(string host)
    {
        if (IPAddress.TryParse(host, out _))
            return true;

        // also treat IPv6 literal without brackets
        return false;
    }

    // ----------------- URL parsing -----------------

    private static bool IsHttpUrl(string url)
        => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
           url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static bool IsHttp(Uri uri)
        => uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
           uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseHttpUri(string url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!IsHttpUrl(url)) return false;
        return Uri.TryCreate(url, UriKind.Absolute, out uri) && IsHttp(uri);
    }

    // ----------------- Levenshtein -----------------

    public static int Levenshtein(string? a, string? b)
    {
        if (a == null) return b?.Length ?? 0;
        if (b == null) return a.Length;

        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        int n = a.Length, m = b.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }

        return d[n, m];
    }

    // ----------------- result model -----------------

    public class PhishingAnalysisResult
    {
        public int Score { get; set; }
        public SuspiciousLevel Level { get; set; }
        public List<string> Issues { get; set; } = [];
        public int TotalLinks { get; set; }
    }

    [GeneratedRegex(@"https?://[^\s""'<>]+")]
    private static partial Regex PlainTextRegex();
}
