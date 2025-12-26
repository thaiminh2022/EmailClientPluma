using EmailClientPluma.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;

namespace EmailClientPluma.Behaviors
{
    internal static class Webview2ColorScheme
    {
        public static readonly DependencyProperty PreferredSchemeProperty =
           DependencyProperty.RegisterAttached(
               "PreferredScheme",
               typeof(AppTheme),
               typeof(Webview2ColorScheme),
               new PropertyMetadata(AppTheme.Auto, OnPreferredSchemeChanged));

        public static void SetPreferredScheme(DependencyObject element, AppTheme value)
            => element.SetValue(PreferredSchemeProperty, value);

        public static AppTheme GetPreferredScheme(DependencyObject element)
            => (AppTheme)element.GetValue(PreferredSchemeProperty);

        private static void OnPreferredSchemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not WebView2 wv) return;

            // Apply now + after init
            wv.Loaded -= WebViewLoaded;
            wv.Loaded += WebViewLoaded;

            wv.CoreWebView2InitializationCompleted -= InitCompleted;
            wv.CoreWebView2InitializationCompleted += InitCompleted;

            _ = ApplyAsync(wv);
        }

        private static void WebViewLoaded(object sender, RoutedEventArgs e)
            => _ = ApplyAsync((WebView2)sender);

        private static void InitCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (sender is WebView2 wv && e.IsSuccess)
                _ = ApplyAsync(wv);
        }

        private static async Task ApplyAsync(WebView2 wv)
        {

            // Ensure CoreWebView2 exists
            if (wv.CoreWebView2 is null)
            {
                try { await wv.EnsureCoreWebView2Async(); }
                catch { return; }
            }

            var scheme = GetPreferredScheme(wv);

            wv.CoreWebView2.Profile.PreferredColorScheme = scheme switch
            {
                AppTheme.Dark => CoreWebView2PreferredColorScheme.Dark,
                AppTheme.Light => CoreWebView2PreferredColorScheme.Light,
                _ => CoreWebView2PreferredColorScheme.Auto,
            };


        }
    }
}
