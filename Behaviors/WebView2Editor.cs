using Microsoft.Web.WebView2.Wpf;
using System.Text.Json;
using System.Windows;

namespace EmailClientPluma.Behaviors
{
    public static class WebView2Editor
    {
        public static readonly DependencyProperty HtmlContentProperty =
            DependencyProperty.RegisterAttached(
                "HtmlContent",
                typeof(string),
                typeof(WebView2Editor),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static void SetHtmlContent(DependencyObject obj, string value)
            => obj.SetValue(HtmlContentProperty, value);

        public static string GetHtmlContent(DependencyObject obj)
            => (string)obj.GetValue(HtmlContentProperty);


        // Call once to hook everything up
        public static readonly DependencyProperty EnableBindingProperty =
            DependencyProperty.RegisterAttached(
                "EnableBinding",
                typeof(bool),
                typeof(WebView2Editor),
                new PropertyMetadata(false, OnEnableBindingChanged));

        public static void SetEnableBinding(DependencyObject obj, bool value)
            => obj.SetValue(EnableBindingProperty, value);

        public static bool GetEnableBinding(DependencyObject obj)
            => (bool)obj.GetValue(EnableBindingProperty);

        private static async void OnEnableBindingChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not WebView2 wv) return;
            if ((bool)e.NewValue == false) return;

            await wv.EnsureCoreWebView2Async();

            // JS → C#: update bound HtmlContent
            wv.CoreWebView2.WebMessageReceived += (s, args) =>
            {
                try
                {
                    var json = JsonDocument.Parse(args.WebMessageAsJson).RootElement;
                    if (json.GetProperty("type").GetString() == "html")
                    {
                        string value = json.GetProperty("value").GetString() ?? "";
                        SetHtmlContent(wv, value);
                    }
                }
                catch
                {
                    // ignore malformed messages
                }
            };


            string editorPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "QuillEditor",
                "index.html");
            wv.Source = new Uri(editorPath);

            // Push initial VM value into editor once loaded
            string current = GetHtmlContent(wv) ?? string.Empty;
            wv.CoreWebView2.NavigationCompleted += async (_, __) =>
            {
                string jsArg = JsonSerializer.Serialize(current);
                await wv.CoreWebView2.ExecuteScriptAsync($"window.setEditorContent({jsArg});");
            };
        }
    }
}
