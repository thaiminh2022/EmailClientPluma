using EmailClientPluma.Core;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Storaging;
using EmailClientPluma.MVVM.ViewModels;
using EmailClientPluma.MVVM.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EmailClientPluma.Core;

namespace EmailClientPluma
{

    public partial class MainView : Window
    {
        public bool IsDarkMode = SettingsView.IsDarkMode;
        public bool IsImg { get; set; } = false;

        public MainView()
        {
            InitializeComponent();
            if (AppSettings.CurrentTheme == AppTheme.Dark)
            {
                ApplyDarkMode();
            }
            else
            {
                ApplyLightMode();
            }

            SettingsView.DarkModeChanged += SettingsView_DarkModeChanged;

            DataContextChanged += (s, e) =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.AllAccountsRemoved += OnAllAccountsRemoved;
                }
            };
        }

        private void OnAllAccountsRemoved()
        {
            // Run on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Resolve StartViewModel from the DI container and set it as DataContext
                var startView = new StartView();

                // If your App exposes the IServiceProvider as Services (common pattern),
                // resolve the view model and assign it so commands and collections initialize properly.
                if (Application.Current is App app && app.Services != null)
                {
                    var vm = app.Services.GetService(typeof(MVVM.ViewModels.StartViewModel)) as MVVM.ViewModels.StartViewModel;
                    if (vm != null)
                        startView.DataContext = vm;
                }

                // If this window is the application's main window, replace it so the app doesn't exit.
                if (Application.Current.MainWindow == this)
                {
                    Application.Current.MainWindow = startView;
                }

                startView.Show();

                // Close the current main view
                this.Close();
            });
        }

        private void SettingsView_DarkModeChanged(object? sender, EventArgs e)
        {
            if (SettingsView.IsDarkMode)
            {
                IsDarkMode = true;
                ApplyDarkMode();
                AppSettings.CurrentTheme = AppTheme.Dark;
            }
            else
            {
                IsDarkMode = false;
                ApplyLightMode();
                AppSettings.CurrentTheme = AppTheme.Light;
            }
        }


        private void ApplyLightMode()
        {
            ThemeHelper.ApplyLight();

            ComposeIcon.Source = new BitmapImage(new Uri("Images/White/pen.png", UriKind.Relative));
            SettingsIcon.Source = new BitmapImage(new Uri("Images/White/settings.png", UriKind.Relative));
            ForwardIcon.Source = new BitmapImage(new Uri("Images/White/arrow_forward.png", UriKind.Relative));
            PreviousIcon.Source = new BitmapImage(new Uri("Images/White/arrow_back.png", UriKind.Relative));

        }

        private void ApplyDarkMode()
        {
            ThemeHelper.ApplyDark();

            ComposeIcon.Source = new BitmapImage(new Uri("Images/Black/pen_black.png", UriKind.Relative));
            SettingsIcon.Source = new BitmapImage(new Uri("Images/Black/settings_black.png", UriKind.Relative));
            ForwardIcon.Source = new BitmapImage(new Uri("Images/Black/arrow_forward_black.png", UriKind.Relative));
            PreviousIcon.Source = new BitmapImage(new Uri("Images/Black/arrow_back_black.png", UriKind.Relative));
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
            LeftColumn.Width = new GridLength(0.5, GridUnitType.Star);
            CenterColumn.Width = new GridLength(2.8, GridUnitType.Star);
            RightColumn.Width = new GridLength(4.2, GridUnitType.Star);
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
        }

        private void MoreSearch_Click(object sender, RoutedEventArgs e)
        {
            MoreSearchPopup.IsOpen = !MoreSearchPopup.IsOpen;
        }
    }
}