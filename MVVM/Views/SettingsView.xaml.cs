using System.Windows;


namespace EmailClientPluma.MVVM.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : Window
    {
        private static bool _isDarkMode;

        public static bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;

                    // Notify listeners
                    DarkModeChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        public static event EventHandler DarkModeChanged;

        public SettingsView()
        {
            InitializeComponent();
        }

        private void LightModeBtn_Click(object sender, RoutedEventArgs e)
        {
            IsDarkMode = false;
        }

        private void DarkModeBtn_Click(object sender, RoutedEventArgs e)
        {
            IsDarkMode = true;
        }
    }
}
