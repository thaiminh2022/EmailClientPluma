using EmailClientPluma.MVVM.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EmailClientPluma
{

    public partial class MainView : Window
    {
        private bool IsDarkMode = SettingsView.IsDarkMode;
        public bool IsImg { get; set; } = false;

        public MainView()
        {
            InitializeComponent();
            ApplyLightMode();
            SettingsView.DarkModeChanged += SettingsView_DarkModeChanged;
        }

        // Light mode values
        private readonly Color Light_Background = (Color)ColorConverter.ConvertFromString("#F7F4FB");
        private readonly Color Light_Panel = (Color)ColorConverter.ConvertFromString("#FAF8FF");
        private readonly Color Light_Primary = (Color)ColorConverter.ConvertFromString("#7E57C2");
        private readonly Color Light_Accent = (Color)ColorConverter.ConvertFromString("#B39DDB");
        private readonly Color Light_Text = Colors.Black;
        private readonly Color Light_ButtonFore = Colors.White;

        // Dark mode values (black + gold buttons)
        private readonly Color Dark_Background = (Color)ColorConverter.ConvertFromString("#000000");
        private readonly Color Dark_Panel = (Color)ColorConverter.ConvertFromString("#3E4042");
        private readonly Color Dark_Accent = (Color)ColorConverter.ConvertFromString("#4B0A66");
        private readonly Color Dark_Text = Colors.White;
        private readonly Color Dark_ButtonBack = (Color)ColorConverter.ConvertFromString("#FFD700"); // gold
        private readonly Color Dark_ButtonFore = Colors.Black; //black text on gold

        // Helper to fetch brush resource and set its Color
        private void SetBrushColor(string key, Color color)
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

        private void SettingsView_DarkModeChanged(object sender, EventArgs e)
        {
            if (SettingsView.IsDarkMode)
            {
                IsDarkMode = true;
                ApplyDarkMode();
                ChangeImgTheme();
            }
            else
            {
                IsDarkMode = false;
                ApplyLightMode();
                ChangeImgTheme();
            }
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

            ComposeIcon.Source = new BitmapImage(new Uri("Images/White/pen.png", UriKind.Relative));
            SettingsIcon.Source = new BitmapImage(new Uri("Images/White/settings.png", UriKind.Relative));
            ForwardIcon.Source = new BitmapImage(new Uri("Images/White/arrow_forward.png", UriKind.Relative));
            PreviousIcon.Source = new BitmapImage(new Uri("Images/White/arrow_back.png", UriKind.Relative));

            ChangeImgTheme();
        }

        private void ApplyDarkMode()
        {
            SetBrushColor("BackgroundBrush", Dark_Background);
            SetBrushColor("PanelBackgroundBrush", Dark_Panel);
            SetBrushColor("PrimaryBrush",
                Dark_ButtonBack); // in dark mode PrimaryBrush used as button background -> gold
            SetBrushColor("AccentBrush", Dark_Accent);
            SetBrushColor("TextBrush", Dark_Text);
            SetBrushColor("ButtonForegroundBrush", Dark_ButtonFore);
            SetBrushColor("GoldBrush", Dark_ButtonBack);

            ComposeIcon.Source = new BitmapImage(new Uri("Images/Black/pen_black.png", UriKind.Relative));
            SettingsIcon.Source = new BitmapImage(new Uri("Images/Black/settings_black.png", UriKind.Relative));
            ForwardIcon.Source = new BitmapImage(new Uri("Images/Black/arrow_forward_black.png", UriKind.Relative));
            PreviousIcon.Source = new BitmapImage(new Uri("Images/Black/arrow_back_black.png", UriKind.Relative));

            ChangeImgTheme();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AccountSettingsPopup.IsOpen = !AccountSettingsPopup.IsOpen;
        }

        private void ChangeContentBtn()
        {

        }

        private void ChangeImgTheme()
        {

        }

        private void EmailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Show the right panel
            RightPanel.Visibility = Visibility.Visible;

            // Resize the first column (list)
            LeftColumn.Width = new GridLength(0.5, GridUnitType.Star);
            CenterColumn.Width = new GridLength(2.8, GridUnitType.Star);
            RightColumn.Width = new GridLength(4.2, GridUnitType.Star);
            ChangeContentBtn();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            // Hide the right panel again
            RightPanel.Visibility = Visibility.Collapsed;
            EmailList.SelectedItem = null;

            // Expand the list to fill all space
            LeftColumn.Width = new GridLength(1, GridUnitType.Star);
            CenterColumn.Width = new GridLength(5, GridUnitType.Star);
            RightColumn.Width = new GridLength(0); // collapse the right side

            IsImg = false;
            ChangeContentBtn();
        }

        private void MoreSearch_Click(object sender, RoutedEventArgs e)
        {
            MoreSearchPopup.IsOpen = !MoreSearchPopup.IsOpen;
        }
    }
}