using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.Core.Services.Storaging;
using EmailClientPluma.MVVM.ViewModels;
using EmailClientPluma.MVVM.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using StorageService = EmailClientPluma.Core.Services.Storaging.StorageService;

namespace EmailClientPluma;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IWindowFactory? factory;
    public App()
    {
        var services = new ServiceCollection();

        // authentication
        services.AddSingleton<IAuthenticationService, GoogleAuthenticationService>();
        services.AddSingleton<MicrosoftAuthenticationService>(); // or AddScoped/AddTransient

        services.AddSingleton<IAuthenticationService>(sp =>
            sp.GetRequiredService<MicrosoftAuthenticationService>());
        services.AddSingleton<IMicrosoftClientApp>(sp =>
            sp.GetRequiredService<MicrosoftAuthenticationService>());

        // storage
        services.AddSingleton<IStorageService, StorageService>();

        // account
        services.AddSingleton<IAccountService, AccountService>();

        // email
        services.AddSingleton<IEmailService, GmailApiEmailService>();
        services.AddSingleton<IEmailService, OutlookApiEmailService>();

        services.AddSingleton<IEmailMonitoringService, EmailMonitoringService>();

        // window
        services.AddSingleton<IWindowFactory, WindowFactory>();

        // Binh's property
        services.AddSingleton<IEmailFilterService, EmailFilterService>();

        // windows
        services.AddTransient<NewEmailViewModel>();
        services.AddTransient<LabelEditorViewModel>();
        services.AddTransient<EmailLabelEditViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<WhichProvViewModel>();
        services.AddTransient<StartViewModel>();

        services.AddSingleton<MainViewModel>();

        Services = services.BuildServiceProvider();
        factory = Services.GetService(typeof(IWindowFactory)) as IWindowFactory;
    }

    public IServiceProvider Services { get; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startWindow = factory?.CreateWindow<StartView, StartViewModel>();
        startWindow?.Show();
    }
}