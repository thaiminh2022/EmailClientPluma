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

            wv2.CoreWebView2.NavigateToString(html);
        }

    }
}
