using EmailClientPluma.MVVM.Views;
using EmailClientPluma.MVVM.ViewModels;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.Core.Services.Storaging;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using EmailClientPluma.Core;
using EmailClientPluma;
using System.Windows.Controls;
using EmailClientPluma.Core.Models;
namespace EmailClientPluma.MVVM.Views
{
    /// <summary>
    /// Interaction logic for StartView.xaml
    /// </summary>
    public partial class StartView : Window
    {
        public static StartView Instance { get; private set; }
        public StartView()
        {
            InitializeComponent();
            Instance = this;
            Loaded += (s, e) => CheckAccounts();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            if (SettingsView.IsDarkMode)
            {
                ThemeHelper.ApplyDark();
            }
            else
            {
                ThemeHelper.ApplyLight();
            }
        }

        private void CheckAccounts()
        {
            if (AccountsListView.Items.Count == 0)
            {
                TitleTextBlock.Text = "Welcome to Pluma!\nSelect an account to continue";
            }
            else
            {
                var Firts = AccountsListView.Items[0];
                var firstAccount = Firts as Account;
                string username = firstAccount.DisplayName;
                TitleTextBlock.Text = $"Welcome back, {username}\nWhat would you like to be as today?";
            }
        }


        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is StartViewModel vm && vm.SelectedAccount != null)
            {
                var mainWindow = new MainView
                {
                    DataContext = ((App)Application.Current).Services.GetRequiredService<MainViewModel>()
                };

                // Pass selected account into MainViewModel
                if (mainWindow.DataContext is MainViewModel mainVm)
                {
                    mainVm.SelectedAccount = vm.SelectedAccount;
                }

                mainWindow.Show();
                this.Close();
            }
        }

    }
}
