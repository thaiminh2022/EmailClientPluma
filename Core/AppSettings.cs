namespace EmailClientPluma.Core
{
    internal static class AppSettings
    {
        public static AppTheme CurrentTheme
        {
            get
            {
                return Properties.Settings.Default.Theme switch
                {
                    "Dark" => AppTheme.Dark,
                    "Light" => AppTheme.Light,
                    _ => AppTheme.Auto
                };
            }
            set
            {
                Properties.Settings.Default.Theme = value switch
                {
                    AppTheme.Light => "Light",
                    AppTheme.Dark => "Dark",
                    _ => "Auto"
                };
                Properties.Settings.Default.Save();
            }
        }

        public static TimeSpan AutoRefreshTime
        {
            get => Properties.Settings.Default.AutoRefreshTime;
            set
            {
                Properties.Settings.Default.AutoRefreshTime = value;
                Properties.Settings.Default.Save();
            }
        }

        public static bool IncreasePollingTimeIfIdleForTooLong
        {
            get => Properties.Settings.Default.IncreasePollingTimeIfIdleForTooLong;
            set
            {
                Properties.Settings.Default.IncreasePollingTimeIfIdleForTooLong = value;
                Properties.Settings.Default.Save();
            }
        }

        public static bool UsePhishingDetector
        {
            get => Properties.Settings.Default.UsePhishingDetector;
            set
            {
                Properties.Settings.Default.UsePhishingDetector = value;
                Properties.Settings.Default.Save();
            }
        }

        public static bool BackgroundMessageSync
        {
            get => Properties.Settings.Default.BackgroundMessageSync;
            set
            {
                Properties.Settings.Default.BackgroundMessageSync = value;
                Properties.Settings.Default.Save();
            }
        }

        public static bool UseBertPhishingDetector
        {
            get => Properties.Settings.Default.UseBERTPhishingDetector;
            set
            {
                Properties.Settings.Default.UseBERTPhishingDetector = value;
                Properties.Settings.Default.Save();
            }
        }

        public static void MigrateSettings()
        {
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }
        }

    }
    public enum AppTheme
    {
        Auto = 0,
        Light = 1,
        Dark = 2
    }

}
