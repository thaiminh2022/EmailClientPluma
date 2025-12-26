using Microsoft.Web.WebView2.Wpf;
using System.Windows;

namespace EmailClientPluma.Behaviors;

public static class WebView2Html
{
    public static readonly DependencyProperty HtmlProperty =
        DependencyProperty.RegisterAttached("Html",
            typeof(string),
            typeof(WebView2Html),
            new PropertyMetadata(null, OnHtmlChanged));


    public static void SetHtml(DependencyObject obj, string value)
    {
        obj.SetValue(HtmlProperty, value);
    }

    public static string GetHtml(DependencyObject obj)
    {
        return (string)obj.GetValue(HtmlProperty);
    }

    private static async void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WebView2 wv2) return;


        var html = e.NewValue as string ?? string.Empty;

        if (wv2.CoreWebView2 is null)
            await wv2.EnsureCoreWebView2Async();

        // Continue viewing email
        wv2.CoreWebView2.NavigateToString(html);
    }
}