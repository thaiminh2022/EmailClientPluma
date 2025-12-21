using EmailClientPluma.MVVM.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EmailClientPluma
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
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
        private readonly Color Dark_Primary = (Color)ColorConverter.ConvertFromString("#2A003D");
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

        private void SettingsView_DarkModeChanged(object sender, EventArgs e) { 
            if (SettingsView.IsDarkMode) {
                IsDarkMode = true;
                ApplyDarkMode();
                ChangeImgTheme();
            } 
            else { 
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
            SetBrushColor("PrimaryBrush", Dark_ButtonBack);     // in dark mode PrimaryBrush used as button background -> gold
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
            if (!IsImg && InboxBtn.Content is string)
            {
                InboxBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/inbox_black.png" : "Images/White/inbox.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                FlagBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/flag_black.png" : "Images/White/flag.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                SentBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/sent_black.png" : "Images/White/sent.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                DraftsBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/drafts_black.png" : "Images/White/drafts.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                ImportantBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/important_black.png" : "Images/White/important.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                SchelduledBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/scheduled_black.png" : "Images/White/scheduled.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                SpamBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/spam_black.png" : "Images/White/spam.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                TrashBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/trash_black.png" : "Images/White/trash.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                AdBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/ad_black.png" : "Images/White/ad.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                SocialBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/social_black.png" : "Images/White/social.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                ForumBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/forum_black.png" : "Images/White/forum.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };

                PromotionBtn.Content = new Image
                {
                    Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/promotion_black.png" : "Images/White/promotion.png", UriKind.Relative)),
                    Width = 24,
                    Height = 24
                };


                CategoryTB.Visibility = Visibility.Collapsed;
                LabelSt.Visibility = Visibility.Collapsed;

                IsImg = true;
            }
            else if (!IsImg && InboxBtn.Content is Image)
            {
                InboxBtn.Content = "Inbox";
                FlagBtn.Content = "Flagged";
                SentBtn.Content = "Sent";
                DraftsBtn.Content = "Drafts";
                ImportantBtn.Content = "Important";
                SchelduledBtn.Content = "Scheduled";
                SpamBtn.Content = "Spam";
                TrashBtn.Content = "Trash";
                AdBtn.Content = "Ad";
                SocialBtn.Content = "Social";
                ForumBtn.Content = "Forum";
                PromotionBtn.Content = "Promotion";

                CategoryTB.Visibility = Visibility.Visible;
                LabelSt.Visibility = Visibility.Visible;
            }
        }

        private void ChangeImgTheme()
        {
            if (InboxBtn.Content is Image img)
            {
                    img.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/inbox_black.png" : "Images/White/inbox.png", UriKind.Relative));
            }
            if (FlagBtn.Content is Image flagImg) { flagImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/flag_black.png" : "Images/White/flag.png", UriKind.Relative)); }
            if (SentBtn.Content is Image sentImg) { sentImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/sent_black.png" : "Images/White/sent.png", UriKind.Relative)); }
            if (DraftsBtn.Content is Image draftsImg) { draftsImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/drafts_black.png" : "Images/White/drafts.png", UriKind.Relative)); }
            if (ImportantBtn.Content is Image importantImg) { importantImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/important_black.png" : "Images/White/important.png", UriKind.Relative)); }
            if (SchelduledBtn.Content is Image scheduledImg) { scheduledImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/scheduled_black.png" : "Images/White/scheduled.png", UriKind.Relative)); }
            if (SpamBtn.Content is Image spamImg) { spamImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/spam_black.png" : "Images/White/spam.png", UriKind.Relative)); }
            if (TrashBtn.Content is Image trashImg) { trashImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/trash_black.png" : "Images/White/trash.png", UriKind.Relative)); }
            if (AdBtn.Content is Image adImg) { adImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/ad_black.png" : "Images/White/ad.png", UriKind.Relative)); }
            if (SocialBtn.Content is Image socialImg) { socialImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/social_black.png" : "Images/White/social.png", UriKind.Relative)); }
            if (ForumBtn.Content is Image forumImg) { forumImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/forum_black.png" : "Images/White/forum.png", UriKind.Relative)); }
            if (PromotionBtn.Content is Image promotionImg) { promotionImg.Source = new BitmapImage(new Uri(IsDarkMode ? "Images/Black/promotion_black.png" : "Images/White/promotion.png", UriKind.Relative)); }
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
            LeftColumn.Width = GridLength.Auto;
            CenterColumn.Width = new GridLength(3, GridUnitType.Star);
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