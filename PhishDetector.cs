using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

public class PhishDetector
{
    public static List<(string href, string display)> ExtractUrlsFromHtml(string html)
    {
        var list = new List<(string href, string display)>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var node in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            string href = node.GetAttributeValue("href", "").Trim();
            string text = WebUtility.HtmlDecode(node.InnerText ?? "").Trim();
            if (!string.IsNullOrEmpty(href))
                list.Add((href, text));
        }

        var textContent = doc.DocumentNode.InnerText;
        foreach (Match m in Regex.Matches(textContent ?? "", @"https?://[^\s""'<>]+"))
        {
            string u = m.Value;
            if (!list.Any(x => x.href == u))
                list.Add((u, u));
        }

        return list;
    }


    public static bool DisplayHrefMismatch((string href, string display) link)
    {
        string? hrefHost = GetHostSafe(link.href);
        string? displayHost = GetHostFromText(link.display);
        if (string.IsNullOrEmpty(hrefHost) || string.IsNullOrEmpty(displayHost)) return false;
        return !hrefHost.Equals(displayHost, StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetHostSafe(string url)
    {
        try
        {
            if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return null;
            var u = new Uri(url);
            return u.Host;
        }
        catch { return null; }
    }

    public static string? GetHostFromText(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var m = Regex.Match(text, @"([a-z0-9\-]+\.[a-z]{2,})", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static int Levenshtein(string a, string b)
    {
        if (a == null) return b?.Length ?? 0;
        if (b == null) return a.Length;
        int n = a.Length, m = b.Length;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[n, m];
    }

    public static int ScoreLinks(IEnumerable<(string href, string display)> links, IEnumerable<string> trustedDomains)
    {
        int score = 0;
        foreach (var link in links)
        {
            if (link.href.Contains("xn--")) score += 30; // punycode
            var host = GetHostSafe(link.href);
            if (host == null) continue;
            if (IPAddress.TryParse(host, out _)) score += 20;
            if (DisplayHrefMismatch(link)) score += 25;

            foreach (var trusted in trustedDomains)
            {
                int dist = Levenshtein(host, trusted);
                if (dist > 0 && dist <= 2) score += 40;
            }
        }
        return score;
    }
}
