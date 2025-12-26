using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.MVVM.Views;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class SettingsViewModel : ObserableObject, IRequestClose
    {
        public bool? BackgroundMessageSync { get; set; } = AppSettings.BackgroundMessageSync;
        public bool? IncreasePollingWhileIdleTooLong { get; set; } = AppSettings.IncreasePollingTimeIfIdleForTooLong;
        public bool? UsePhishingDetector { get; set; } = AppSettings.UsePhishingDetector;
        public bool? UseBertPhishingDetector { get; set; } = AppSettings.UseBertPhishingDetector;

        private int _autoRefreshSecs = AppSettings.AutoRefreshTime.Seconds;

        public string AutoRefreshSecs
        {
            get => _autoRefreshSecs.ToString();
            set
            {
                _autoRefreshSecs = int.TryParse(value, out var secs) ? secs : 30;
                OnPropertyChanges();
            }
        }

        public ObservableCollection<Account> Accounts { get; set; }
        public ICollectionView GoogleAccounts { get; set; }
        public ICollectionView MicrosoftAccounts { get; set; }
        public Account? SelectedAccount { get; set; }


        public event EventHandler<bool?>? RequestClose;
        public RelayCommand AddAccountCommand {get; set;}
        public RelayCommandAsync RemoveGoogleAccountCommand {get; set;}
        public RelayCommandAsync RemoveMicrosoftAccountCommand { get; set; }

        public RelayCommand OpenDatabaseFolder {get; set;}
        public RelayCommand OpenLogFolder {get; set;}
        public RelayCommand DeleteLogFolder { get; set; }
        public RelayCommand SaveCommand { get; set; }

        public SettingsViewModel(IWindowFactory windowFactory, IAccountService accountService, ILogger<SettingsViewModel> logger)
        {

            Accounts = accountService.GetAccounts();
            GoogleAccounts = new ListCollectionView(Accounts);
            GoogleAccounts.Filter = x => x is Account { Provider: Provider.Google };
            
            MicrosoftAccounts = new ListCollectionView(Accounts);
            MicrosoftAccounts.Filter = x => x is Account { Provider: Provider.Microsoft };


            // accounts
            AddAccountCommand = new RelayCommand(async _ =>
            {
                var win = windowFactory.CreateWindow<WhichProvView, WhichProvViewModel>();
                win.Show();
            });
            RemoveGoogleAccountCommand = new RelayCommandAsync(async _ =>
            {
                if (SelectedAccount is null || SelectedAccount.Provider != Provider.Google) return;
                await RemoveAccount(SelectedAccount, accountService);

            }, _ => SelectedAccount is not null);

            RemoveMicrosoftAccountCommand = new RelayCommandAsync(async _ =>
            {
                if (SelectedAccount is null || SelectedAccount.Provider != Provider.Microsoft) return;
                await RemoveAccount(SelectedAccount, accountService);

            }, _ => SelectedAccount is not null);

            OpenDatabaseFolder = new RelayCommand(_ =>
            {
                var psi = new ProcessStartInfo()
                {
                    FileName = "explorer.exe",
                    Arguments = $"""
                                "{Helper.DataFolder}"
                                """,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                logger.LogInformation("Running process with args: {args}", psi.Arguments);
                Process.Start(psi);
            });

            OpenLogFolder = new RelayCommand(_ =>
            {

                var psi = new ProcessStartInfo()
                {
                    FileName = "explorer.exe",
                    Arguments = $"""
                                 "{Helper.LogFolder}"
                                 """,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                logger.LogInformation("Running process with args: {args}", psi.Arguments);

                Process.Start(psi);
            });

            DeleteLogFolder = new RelayCommand(_ =>
            {
                var result =
                    MessageBoxHelper.Confirmation("Deleting the log folder requires application shutdown, continue?");

                if (result is not true) return;

                // flush whatever is left and dispose
                Log.CloseAndFlush();
                Directory.Delete(Helper.LogFolder, true);
                Application.Current.Shutdown(9);
            });

            SaveCommand = new RelayCommand(_ =>
            {
                logger.LogInformation("Saving new settings");
                AppSettings.IncreasePollingTimeIfIdleForTooLong = IncreasePollingWhileIdleTooLong ?? AppSettings.IncreasePollingTimeIfIdleForTooLong;
                AppSettings.UsePhishingDetector  = UsePhishingDetector  ?? AppSettings.UsePhishingDetector ;
                AppSettings.UseBertPhishingDetector = UseBertPhishingDetector ?? AppSettings.UseBertPhishingDetector;

                if (_autoRefreshSecs < 30)
                {
                    logger.LogWarning("Saving settings failed due to auto refresh time {sec}< 30 seconds", _autoRefreshSecs);
                    MessageBoxHelper.Warning("less than 30 seconds is too small, api consumption may explode. Consider 30 seconds or larger");
                    return;
                }
                AppSettings.AutoRefreshTime = TimeSpan.FromSeconds(_autoRefreshSecs);


                if (BackgroundMessageSync != AppSettings.BackgroundMessageSync)
                {
                    var result = MessageBoxHelper.Confirmation(
                        "Changing background message sync requires application shutdown, continue?");

                    if (result is true)
                    {
                        logger.LogInformation("Settings saved successfully");
                        logger.LogWarning("Application shutdown due to background message sync changed");
                        AppSettings.BackgroundMessageSync = BackgroundMessageSync ?? AppSettings.BackgroundMessageSync;
                        Application.Current.Shutdown();
                        return;
                    }
                }

                logger.LogInformation("Settings saved successfully");
                RequestClose?.Invoke(this, true);
            });

            
        }
        async Task RemoveAccount(Account acc, IAccountService accountService)
        {
            var result = MessageBoxHelper.Confirmation("Do you want to delete ", acc.Email);

            try
            {
                if (result is true)
                {
                    await accountService.RemoveAccountAsync(acc);
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Error(ex.Message);
            }
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public SettingsViewModel()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
        }
    }
}
