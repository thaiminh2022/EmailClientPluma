using System.IO;

namespace EmailClientPluma.Core
{
    internal static class AppPaths
    {
        public static string DataFolder => GetDataFolder();
        public static string DatabasePath => Path.Combine(DataFolder, "pluma.db");
        public static string ClientSecretPathDev = @"secrets/secret.json";

        static string GetDataFolder()
        {
            var path = Path.Combine(Environment.GetFolderPath(
                                                    Environment.SpecialFolder.ApplicationData), "Pluma");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
