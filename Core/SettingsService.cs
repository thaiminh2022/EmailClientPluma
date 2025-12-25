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

    }
    public enum AppTheme
    {
        Auto = 0,
        Light = 1,
        Dark = 2
    }

}
