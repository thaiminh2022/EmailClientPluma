using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EmailClientPluma;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainView : Window
{
    private readonly Color Dark_Accent = (Color)ColorConverter.ConvertFromString("#4B0A66");

    // Dark mode values (black + gold buttons)
    private readonly Color Dark_Background = (Color)ColorConverter.ConvertFromString("#000000");
    private readonly Color Dark_ButtonBack = (Color)ColorConverter.ConvertFromString("#FFD700"); // gold
    private readonly Color Dark_ButtonFore = Colors.Black; //black text on gold
    private readonly Color Dark_Panel = (Color)ColorConverter.ConvertFromString("#3E4042");
    private readonly Color Dark_Primary = (Color)ColorConverter.ConvertFromString("#2A003D");
    private readonly Color Dark_Text = Colors.White;
    private readonly Color Light_Accent = (Color)ColorConverter.ConvertFromString("#B39DDB");

    // Light mode values
    private readonly Color Light_Background = (Color)ColorConverter.ConvertFromString("#F7F4FB");
    private readonly Color Light_ButtonFore = Colors.White;
    private readonly Color Light_Panel = (Color)ColorConverter.ConvertFromString("#FAF8FF");
    private readonly Color Light_Primary = (Color)ColorConverter.ConvertFromString("#7E57C2");
    private readonly Color Light_Text = Colors.Black;

    public MainView()
    {
        InitializeComponent();
        ApplyLightMode();
    }

    // Helper to fetch brush resource and set its Color
    private void SetBrushColor(string key, Color color)
    {
        if (TryFindResource(key) is SolidColorBrush originalBrush)
        {
            if (originalBrush.IsFrozen)
            {
                var newBrush = originalBrush.Clone();
                newBrush.Color = color;
                Resources[key] = newBrush;
            }
            else
            {
                originalBrush.Color = color;
            }
        }
        else if (Application.Current.Resources.Contains(key) &&
                 Application.Current.Resources[key] is SolidColorBrush appBrush)
        {
            if (appBrush.IsFrozen)
            {
                var newBrush = appBrush.Clone();
                newBrush.Color = color;
                Application.Current.Resources[key] = newBrush;
            }
            else
            {
                appBrush.Color = color;
            }
        }
    }

    private void LightModeBtn_Click(object sender, RoutedEventArgs e)
    {
        ApplyLightMode();
    }

    private void DarkModeBtn_Click(object sender, RoutedEventArgs e)
    {
        ApplyDarkMode();
    }

    private void ApplyLightMode()
    {
        SetBrushColor("BackgroundBrush", Light_Background);
        SetBrushColor("PanelBackgroundBrush", Light_Panel);
        SetBrushColor("PrimaryBrush", Light_Primary);
        SetBrushColor("AccentBrush", Light_Accent);
        SetBrushColor("TextBrush", Light_Text);
        SetBrushColor("ButtonForegroundBrush", Light_ButtonFore);
        SetBrushColor("GoldBrush", (Color)ColorConverter.ConvertFromString("#FFD700"));
    }

    private void ApplyDarkMode()
    {
        SetBrushColor("BackgroundBrush", Dark_Background);
        SetBrushColor("PanelBackgroundBrush", Dark_Panel);
        SetBrushColor("PrimaryBrush", Dark_ButtonBack); // in dark mode PrimaryBrush used as button background -> gold
        SetBrushColor("AccentBrush", Dark_Accent);
        SetBrushColor("TextBrush", Dark_Text);
        SetBrushColor("ButtonForegroundBrush", Dark_ButtonFore);
        SetBrushColor("GoldBrush", Dark_ButtonBack);
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemePopup.IsOpen = !ThemePopup.IsOpen;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        AccountSettingsPopup.IsOpen = !AccountSettingsPopup.IsOpen;
    }

    private void EmailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Show the right panel
        RightPanel.Visibility = Visibility.Visible;

        // Resize the first column (list)
        CenterColumn.Width = new GridLength(1.2, GridUnitType.Star);
        RightColumn.Width = new GridLength(3.8, GridUnitType.Star);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        // Hide the right panel again
        RightPanel.Visibility = Visibility.Collapsed;
        EmailList.SelectedItem = null;

        // Expand the list to fill all space
        CenterColumn.Width = new GridLength(3, GridUnitType.Star);
        RightColumn.Width = new GridLength(0); // collapse the right side
    }

    private void MoreSearch_Click(object sender, RoutedEventArgs e)
    {
        MoreSearchPopup.IsOpen = !MoreSearchPopup.IsOpen;
    }
}