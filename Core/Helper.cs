using System.IO;
using System.Windows.Media;

namespace EmailClientPluma.Core;

internal static class Helper
{
    public static string DataFolder => GetDataFolder();
    public static string MsalCachePath => Path.Combine(DataFolder, "msal-cache.bin");
    public static string DatabasePath => Path.Combine(DataFolder, "pluma.db");
    public static string LogFolder => GetLogFolder();
    public static string AttachmentsFolder => GetAttachmentFolder();


    

    private static string GetDataFolder()
    {
        var path = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "Pluma");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetLogFolder()
    {
        var dataFolder = GetDataFolder();
        var path = Path.Combine(dataFolder, "log");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetAttachmentFolder()
    {
        var path = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments), "PlumaAttachments");
        Directory.CreateDirectory(path);
        return path;
    }

    public static Color ColorFromArgb(int argb)
    {
        var a = (byte)((argb >> 24) & 0xFF);
        var r = (byte)((argb >> 16) & 0xFF);
        var g = (byte)((argb >> 8) & 0xFF);
        var b = (byte)(argb & 0xFF);
        return Color.FromArgb(a, r, g, b);
    }

    public static int ColorToArgb(Color c)
    {
        return (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
    }
}