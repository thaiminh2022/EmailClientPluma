using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.MVVM.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace EmailClientPluma
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Services { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainView()
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
            mainWindow.Show();
        }

        public App()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IAuthenticationService, GoogleAuthenticationService>();
            services.AddSingleton<IStorageService, StorageService>();
            services.AddSingleton<IAccountService, AccountService>();


            // Might change this later, it's singleton due to aplication design
            services.AddSingleton<MainViewModel>();

            Services = services.BuildServiceProvider();
        }
    }

}
