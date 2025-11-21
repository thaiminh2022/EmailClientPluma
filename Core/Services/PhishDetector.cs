using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace EmailClientPluma.Core.Services
{
    using HrefDisplay = (string href, string display);

    public static class PhishDetector
    {
        public static List<HrefDisplay> ExtractUrlsFromHtml(string html)
        {
            var list = new List<HrefDisplay>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var doc = new HtmlDocument();
            doc.LoadHtml(html ?? string.Empty);

            // 1. <a href="...">display</a>
            foreach (var node in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
            {
                string href = node.GetAttributeValue("href", "").Trim();
                string text = WebUtility.HtmlDecode(node.InnerText ?? "").Trim();

                if (!string.IsNullOrEmpty(href) &&
                    IsHttpUrl(href) &&
                    seen.Add(href))
                {
                    list.Add((href, text));
                }
            }

            // 2. Plain text URLs
            var textContent = doc.DocumentNode.InnerText ?? string.Empty;
            foreach (Match m in Regex.Matches(textContent, @"https?://[^\s""'<>]+"))
            {
                string u = m.Value;
                if (IsHttpUrl(u) && seen.Add(u))
                    list.Add((u, u));
            }

            return list;
        }

        public static bool DisplayHrefMismatch(HrefDisplay link)
        {
            string? hrefHost = GetHostSafe(link.href);
            string? displayHost = GetHostFromText(link.display);

            if (string.IsNullOrEmpty(hrefHost) || string.IsNullOrEmpty(displayHost))
                return false;

            string hrefBase = GetBaseDomain(hrefHost);
            string displayBase = GetBaseDomain(displayHost);

            if (string.IsNullOrEmpty(hrefBase) || string.IsNullOrEmpty(displayBase))
                return false;

            return !hrefBase.Equals(displayBase, StringComparison.OrdinalIgnoreCase);
        }

        public static string? GetHostSafe(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!IsHttpUrl(url))
                return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            var host = uri.Host?.TrimEnd('.');
            return string.IsNullOrEmpty(host) ? null : host.ToLowerInvariant();
        }

        public static string? GetHostFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // Heuristic: grab something that looks like a domain
            var m = Regex.Match(text, @"([a-z0-9\-]+\.[a-z0-9\.\-]{2,})", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
        }

        public static int Levenshtein(string a, string b)
        {
            if (a == null) return b?.Length ?? 0;
            if (b == null) return a.Length;

            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();

            int n = a.Length, m = b.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }

            return d[n, m];
        }

        public static int ScoreLinks(IEnumerable<HrefDisplay> links, IEnumerable<string> trustedDomains)
        {
            int totalScore = 0;

            // Normalize trusted domains to base domains once
            var trustedBaseDomains = (trustedDomains ?? Array.Empty<string>())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => GetBaseDomain(d.ToLowerInvariant()))
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var link in links)
            {
                int score = 0;

                // Punycode / IDN
                if (link.href.Contains("xn--", StringComparison.OrdinalIgnoreCase))
                    score += 30;

                var host = GetHostSafe(link.href);
                if (host == null)
                    continue;

                string baseDomain = GetBaseDomain(host);

                // Raw IP host
                if (IPAddress.TryParse(host, out _))
                    score += 20;

                // Display vs actual mismatch
                if (DisplayHrefMismatch(link))
                    score += 25;

                // Typo-squatting: base domain similar to a trusted domain
                if (!string.IsNullOrEmpty(baseDomain) && trustedBaseDomains.Count > 0)
                {
                    int? closestDist = null;

                    foreach (var trustedBase in trustedBaseDomains)
                    {
                        int dist = Levenshtein(baseDomain, trustedBase);
                        if (closestDist == null || dist < closestDist.Value)
                            closestDist = dist;
                    }

                    if (closestDist is int cd && cd > 0 && cd <= 2)
                        score += 40;
                }

                totalScore += score;
            }

            return totalScore;
        }

        // ---------- private helpers ----------

        private static bool IsHttpUrl(string url)
        {
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Good enough for most cases (google.com, microsoft.com, vnpt.com.vn is not perfect).
        /// </summary>
        private static string GetBaseDomain(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return host;

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 2)
                return host.ToLowerInvariant();

            // last two labels
            return string.Join('.', parts[^2], parts[^1]).ToLowerInvariant();
        }

        public enum SuspiciousLevel
        {
            None,
            Minor,
            Medium,
            Major,
        }
        public static SuspiciousLevel ValidateHtmlContent(string html)
        {
            var links = ExtractUrlsFromHtml(html);
            List<string> trustedDomains = [
                // --- Email providers ---
                "googleusercontent.com", "outlook.com", "hotmail.com", "live.com", "office.com",
                "microsoft.com", "icloud.com", "me.com", "yahoo.com","google.com",
                "yahoo.co.jp", "proton.me", "protonmail.com","gmail.com", 

                // --- Identity / authentication ---
                "accounts.google.com", "login.microsoftonline.com", 
                "appleid.apple.com", "auth0.com", "okta.com",

                // --- Banks & payment (global) ---
                "paypal.com", "stripe.com", "visa.com", "mastercard.com", "americanexpress.com",

                // --- E-commerce ---
                "amazonaws.com", "alibaba.com", "aliexpress.com", 
                "ebay.com", "shopify.com", "amazon.com",

                // --- Social media ---
                "facebook.com", "fb.com", "messenger.com",
                "instagram.com", "tiktok.com", "x.com", "twitter.com", 
                "linkedin.com", "reddit.com", "discord.com",

                // --- Developer / code hosting ---
                "github.com", "githubusercontent.com", "gitlab.com", "bitbucket.org", 
                "stackexchange.com", "stackoverflow.com", "cloudflare.com", "vercel.com",

                // --- Cloud platforms ---
                "azure.com", "microsoftonline.com", "windows.net", "aws.amazon.com", "amazonaws.com",
                "cloudfront.net", "firebaseapp.com", "firebaseio.com", "oracle.com",

                // --- Video / conferencing ---
                "youtube.com", "youtu.be", "zoom.us", "teams.microsoft.com", "skype.com",

                // --- VN local services ---
                "zalo.me", "zalo.me", "vnexpress.net", "vnpt.vn", "vnpt.com.vn", "viettel.com.vn",
                "vng.com.vn", "zing.vn", "tiki.vn", "shopee.vn", "lazada.vn",
            ];


            return ScoreLinks(links, trustedDomains) switch
            {
                0 => SuspiciousLevel.None,
                < 40 => SuspiciousLevel.Minor,
                >= 40 and < 80 => SuspiciousLevel.Medium,
                >= 80 => SuspiciousLevel.Major,
            };
        }
    }
}
