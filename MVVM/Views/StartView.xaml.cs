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
