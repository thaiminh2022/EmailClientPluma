using Microsoft.Web.WebView2.Wpf;
using System.Windows;

namespace EmailClientPluma.Behaviors
{
    public static class WebView2Html
    {
        public static readonly DependencyProperty HtmlProperty =
            DependencyProperty.RegisterAttached("Html",
            typeof(string),
            typeof(WebView2Html),
            new PropertyMetadata(null, OnHtmlChanged));


        public static void SetHtml(DependencyObject obj, string value) => obj.SetValue(HtmlProperty, value);
        public static string GetHtml(DependencyObject obj) => (string)obj.GetValue(HtmlProperty);

        private async static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not WebView2 wv2) return;
            string? html = e.NewValue as string ?? string.Empty;

            if (wv2.CoreWebView2 == null)
                await wv2.EnsureCoreWebView2Async();

            // if html is invalid, tell them it's in valid
            if (!html.Contains("<base", System.StringComparison.OrdinalIgnoreCase))
                html = html.Replace("<head>", "<head><base href=\"https://app.invalid/\">");
            //Check phising before view email
            var links = PhishDetector.ExtractUrlsFromHtml(html);
            var trustedDomains = new[] { "gmail.com", "outlook.com", "apple.com", "bank.com" };
            int score = PhishDetector.ScoreLinks(links, trustedDomains);

            if (score >= 80)
            {
                MessageBox.Show(
                    "WARNING: This email shows signs of phishing!\nBe careful when clicking on links.",
                    "Security alert",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            else if (score >= 50)
            {
                MessageBox.Show(
                    "This email has some suspicious signs.",
                    "Low warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            // ----------------------------------------------------

            // Continue viewing email
            wv2.CoreWebView2.NavigateToString(html);
        }

    }
}
