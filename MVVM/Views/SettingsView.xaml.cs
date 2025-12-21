using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EmailClientPluma.MVVM.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : Window
    {
        private static bool _isDarMode;
        public static bool IsDarkMode
        {
            get { return _isDarMode; }
            set
            {
                if (_isDarMode != value)
                {
                    _isDarMode = value;
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
