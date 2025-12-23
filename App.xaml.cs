using System.IO;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;
using EmailClientPluma.Core.Services.Storaging;
using EmailClientPluma.MVVM.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using EmailClientPluma.Core;
using Serilog;
using Serilog.Events;

namespace EmailClientPluma;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        var services = new ServiceCollection();

        AddLogging(services);
        AddServices(services);

        Services = services.BuildServiceProvider();
    }

    private void AddLogging(ServiceCollection services)
    {
        var runId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .WriteTo.Debug()
            .WriteTo.Async(config => config.File(
                path: Path.Combine(Helper.LogFolder, $"app-{runId}.log"),
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10_000_000,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
                ))
            .CreateLogger();
        services.AddLogging(config =>
        {
            config.ClearProviders();
            config.AddSerilog(Log.Logger, dispose: true);
        });
    }

    private static void AddServices(ServiceCollection services)
    {
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
        
        services.AddSingleton<IEmailFilterService, EmailFilterService>();

        // window
        services.AddSingleton<IWindowFactory, WindowFactory>();


        //window
        services.AddTransient<NewEmailViewModel>();
        services.AddTransient<LabelEditorViewModel>();
        services.AddTransient<EmailLabelEditViewModel>();

        services.AddSingleton<MainViewModel>();

        services.AddTransient<SettingsViewModel>();

        services.AddTransient<WhichProvViewModel>();
    }

    public IServiceProvider Services { get; }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var mainWindow = new MainView
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }
}