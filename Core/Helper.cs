using EmailClientPluma.Core.Models;
using System.IO;
using System.Windows.Media;

namespace EmailClientPluma.Core;

internal static class Helper
{
    public static string DataFolder => GetDataFolder();
    public static string MsalCachePath => Path.Combine(DataFolder, "msal-cache.bin");
    public static string DatabasePath => Path.Combine(DataFolder, "pluma.db");
    public static string LogFolder => GetLogFolder();


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
        var path = Path.Combine(dataFolder, "log/");
        Directory.CreateDirectory(path);
        return path;
    }

    public static bool IsEmailEqual(Email a, Email b)
    {
        return string.Equals(a.MessageIdentifiers.OwnerAccountId, b.MessageIdentifiers.OwnerAccountId) &&
               string.Equals(a.MessageIdentifiers.ProviderMessageId, b.MessageIdentifiers.ProviderMessageId);
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