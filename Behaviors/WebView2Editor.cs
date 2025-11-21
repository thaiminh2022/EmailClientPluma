using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;
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
            MessageBox.Show("Load");
            if (d is not WebView2 wv2) return;
            string html = e.NewValue as string ?? "";

            await wv2.EnsureCoreWebView2Async();

            // ---- Load local editor (only once) ----
            if (EditorDocumentReady(wv2.CoreWebView2))
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "QuillEditor",               
                    "index.html"          
                );

                wv2.CoreWebView2.Navigate(path);
                await WaitForDocumentAsync(wv2);
            }

            await wv2.CoreWebView2.ExecuteScriptAsync($"window.setEditorHtml({html});");
        }

        static bool EditorDocumentReady(CoreWebView2 core) { 
            return core.Source?.EndsWith("index.html") == true;
        }

        private static Task WaitForDocumentAsync(WebView2 w)
        {
            var tcs = new TaskCompletionSource();
            w.CoreWebView2.NavigationCompleted += Handler;
            void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
            {
                w.CoreWebView2.NavigationCompleted -= Handler;
                tcs.TrySetResult();
            }
            return tcs.Task;
        }

        private static async Task EnsureInitializedAsync(WebView2 webView)
        {
            if (webView.CoreWebView2 == null)
            {
                await webView.EnsureCoreWebView2Async();
            }
        }
    }
}
