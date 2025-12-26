using EmailClientPluma.Core.Models;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace EmailClientPluma.Behaviors;

internal class WebView2SelectEditor
{
    public static readonly DependencyProperty HtmlContentProperty =
        DependencyProperty.RegisterAttached(
            "HtmlContent",
            typeof(string),
            typeof(WebView2SelectEditor),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


    public static readonly DependencyProperty EmailContentProperty =
        DependencyProperty.RegisterAttached(
            "EmailContent",
            typeof(Email),
            typeof(WebView2SelectEditor));


    // Call once to hook everything up
    public static readonly DependencyProperty EnableBindingProperty =
        DependencyProperty.RegisterAttached(
            "EnableBinding",
            typeof(bool),
            typeof(WebView2SelectEditor),
            new PropertyMetadata(false, OnEnableBindingChanged));

    public static void SetHtmlContent(DependencyObject obj, string value)
    {
        obj.SetValue(HtmlContentProperty, value);
    }

    public static string GetHtmlContent(DependencyObject obj)
    {
        return (string)obj.GetValue(HtmlContentProperty);
    }


    public static async Task PushEmailToWebViewAsync(WebView2 wv, Email mail)
    {
        await wv.EnsureCoreWebView2Async();

        var msp = mail.MessageParts;
        var subject = JsonSerializer.Serialize(mail.MessageParts.Subject);
        var body = JsonSerializer.Serialize(mail.BodyFetched ? mail.MessageParts.Body : string.Empty);
        var meta = JsonSerializer.Serialize(new
        {
            from = msp.From,
            to = msp.To,
            date = msp.DateDisplay
        });

        await wv.CoreWebView2.ExecuteScriptAsync($"window.setEmailContent({subject}, {meta}, {body})");
    }

    public static void SetEmailContent(DependencyObject obj, Email value)
    {
        obj.SetValue(EmailContentProperty, value);
    }

    public static Email? GetEmailContent(DependencyObject obj)
    {
        return (Email?)obj.GetValue(EmailContentProperty);
    }

    public static void SetEnableBinding(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableBindingProperty, value);
    }

    public static bool GetEnableBinding(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableBindingProperty);
    }

    private static async void OnEnableBindingChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WebView2 wv) return;
        if (!(bool)e.NewValue) return;

        await wv.EnsureCoreWebView2Async();

        // JS → C#: update bound HtmlContent
        wv.CoreWebView2.WebMessageReceived += (s, args) =>
        {
            try
            {
                var json = JsonDocument.Parse(args.WebMessageAsJson).RootElement;
                if (json.GetProperty("type").GetString() == "html")
                {
                    var value = json.GetProperty("value").GetString() ?? "";
                    SetHtmlContent(wv, value);
                }
            }
            catch
            {
                // ignore broken messages
            }
        };


        var editorPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "QuillEditor",
            "selectReplyEditor.html");
        wv.Source = new Uri(editorPath);

        var current = GetHtmlContent(wv) ?? string.Empty;
        wv.CoreWebView2.NavigationCompleted += async (_, __) =>
        {
            // Push initial value into editor once loaded
            var jsArg = JsonSerializer.Serialize(current);
            await wv.CoreWebView2.ExecuteScriptAsync($"window.setEditorContent({jsArg});");

            // Push current email if any
            var currentEmail = GetEmailContent(wv);
            if (currentEmail is not null) await PushEmailToWebViewAsync(wv, currentEmail);
        };
    }
}