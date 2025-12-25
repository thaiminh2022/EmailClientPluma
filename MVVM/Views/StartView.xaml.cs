using EmailClientPluma.MVVM.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using EmailClientPluma.Core;
using System.Windows.Controls;
using EmailClientPluma.Core.Models;
namespace EmailClientPluma.MVVM.Views
{
    /// <summary>
    /// Interaction logic for StartView.xaml
    /// </summary>
    public partial class StartView : Window
    {
        public StartView()
        {
            InitializeComponent();
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
                var first = AccountsListView.Items[0];
                var firstAccount = first as Account;
                string username = firstAccount?.DisplayName ?? "";
                TitleTextBlock.Text = $"Welcome back, {username}\nWhat would you like to be as today?";
            }
        }

    }
}
