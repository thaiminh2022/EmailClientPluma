using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Windows;

namespace EmailClientPluma.Behaviors
{
    internal class WebView2Editor
    {
        public static readonly DependencyProperty HtmlProperty =
           DependencyProperty.RegisterAttached(
               "Html",
               typeof(string),
               typeof(WebView2Editor),
               new PropertyMetadata(null, OnHtmlChanged));


        public static string GetHtml(DependencyObject obj) =>
            (string)obj.GetValue(HtmlProperty);

        public static void SetHtml(DependencyObject obj, string value) =>
            obj.SetValue(HtmlProperty, value);

        private static async void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not WebView2 wv2) return;
            string html = e.NewValue as string ?? "";
            await wv2.EnsureCoreWebView2Async();
            await wv2.CoreWebView2.ExecuteScriptAsync($"window.setEditorHtml({html});");
        }


        private static async Task EnsureInitializedAsync(WebView2 wv2)
        {
            if (wv2.CoreWebView2 == null)
            {
                await wv2.EnsureCoreWebView2Async();
            }

            if (wv2.CoreWebView2 is null)
                return;

            string filePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "QuillEditor",
                "index.html"
            );

            var uri = new Uri(Path.GetFullPath(filePath));
            wv2.CoreWebView2.Navigate(uri.AbsoluteUri);

            wv2.WebMessageReceived += (s, e) =>
            {
                string? htmlFromJs = e.TryGetWebMessageAsString();
                if (htmlFromJs is null) return;

                string current = GetHtml(wv2) ?? string.Empty;
                if (current.Equals(htmlFromJs))
                    return; // nothing changed

                SetHtml(wv2, htmlFromJs);
            };
        }
    }
}
