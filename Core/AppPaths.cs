using System.IO;

namespace EmailClientPluma.Core
{
    internal static class AppPaths
    {
        public readonly static string DataFolder = Path.Combine(Environment.GetFolderPath(
                                                    Environment.SpecialFolder.ApplicationData), "Pluma");
        public readonly static string DatabasePath = Path.Combine(DataFolder, "pluma.db");
        public static string ClientSecretPathDev = @"secrets/secret.json";
    }
}
