using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.Core.Services.Storaging;
using EmailClientPluma.MVVM.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using StorageService = EmailClientPluma.Core.Services.Storaging.StorageService;

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
            services.AddSingleton<IEmailService, EmailService>();
            services.AddSingleton<IWindowFactory, WindowFactory>();
            services.AddSingleton<IEmailMonitoringService, EmailMonitoringService>();

            //Binhs property
            services.AddSingleton<IEmailFilterService, EmailFilterService>();

            //window
            services.AddTransient<NewEmailViewModel>();
            services.AddTransient<LabelEditorViewModel>();
            services.AddTransient<EmailLabelEditViewModel>();

            // Might change this later, it's a singleton due to application design
            services.AddSingleton<MainViewModel>();

            services.AddTransient<SettingsViewModel>();

            services.AddTransient<WhichProvViewModel>();

            Services = services.BuildServiceProvider();
        }
    }

}
