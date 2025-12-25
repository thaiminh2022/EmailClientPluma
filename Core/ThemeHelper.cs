using EmailClientPluma.MVVM.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EmailClientPluma.Core
{
    public static class ThemeHelper
    {
        // Light mode values
        private static readonly Color Light_Background = (Color)ColorConverter.ConvertFromString("#F7F4FB");
        private static readonly Color Light_Panel = (Color)ColorConverter.ConvertFromString("#FAF8FF");
        private static readonly Color Light_Primary = (Color)ColorConverter.ConvertFromString("#7E57C2");
        private static readonly Color Light_Accent = (Color)ColorConverter.ConvertFromString("#B39DDB");
        private static readonly Color Light_Text = Colors.Black;
        private static readonly Color Light_ButtonFore = Colors.White;

        // Dark mode values (black + gold buttons)
        private static readonly Color Dark_Background = (Color)ColorConverter.ConvertFromString("#000000");
        private static readonly Color Dark_Panel = (Color)ColorConverter.ConvertFromString("#3E4042");
        private static readonly Color Dark_Accent = (Color)ColorConverter.ConvertFromString("#4B0A66");
        private static readonly Color Dark_Text = Colors.White;
        private static readonly Color Dark_ButtonBack = (Color)ColorConverter.ConvertFromString("#FFD700");
        private static readonly Color Dark_ButtonFore = Colors.Black;

        // Helper to fetch brush resource and set its Color
        private static void SetBrushColor(string key, Color color)
        {
            if (Application.Current.Resources[key] is SolidColorBrush brush)
            {
                if (brush.IsFrozen)
                {
                    var clone = brush.Clone();
                    clone.Color = color;
                    Application.Current.Resources[key] = clone;
                }
                else
                {
                    brush.Color = color;
                }
            }
        }

        public static void ApplyLight()
        {
            SetBrushColor("BackgroundBrush", Light_Background);
            SetBrushColor("PanelBackgroundBrush", Light_Panel);
            SetBrushColor("PrimaryBrush", Light_Primary);
            SetBrushColor("AccentBrush", Light_Accent);
            SetBrushColor("TextBrush", Light_Text);
            SetBrushColor("ButtonForegroundBrush", Light_ButtonFore);
            SetBrushColor("GoldBrush", (Color)ColorConverter.ConvertFromString("#FFD700"));
        }

        public static void ApplyDark()
        {
            SetBrushColor("BackgroundBrush", Dark_Background);
            SetBrushColor("PanelBackgroundBrush", Dark_Panel);
            SetBrushColor("PrimaryBrush", Dark_ButtonBack); // in dark mode PrimaryBrush used as button background -> gold
            SetBrushColor("AccentBrush", Dark_Accent);
            SetBrushColor("TextBrush", Dark_Text);
            SetBrushColor("ButtonForegroundBrush", Dark_ButtonFore);
            SetBrushColor("GoldBrush", Dark_ButtonBack);
        }
    }
}
