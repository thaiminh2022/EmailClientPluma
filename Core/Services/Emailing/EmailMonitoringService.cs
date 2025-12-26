using EmailClientPluma.Core.Models;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace EmailClientPluma.Core.Services.Emailing;

internal interface IEmailMonitoringService
{
    void StartMonitor(Account acc);
    void StopMonitor(Account acc);
}

internal class EmailMonitoringService(IEnumerable<IEmailService> emailServices, ILogger<EmailMonitoringService> logger) : IEmailMonitoringService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, AccountMonitor> _monitors = [];
    private readonly List<IEmailService> _emailServices = [.. emailServices];

    public void StopMonitor(Account acc)
    {
        lock (_lock)
        {
            if (!_monitors.TryGetValue(acc.ProviderUID, out var monitor)) return;

            monitor.Cancellation.Cancel();
            monitor.Cancellation.Dispose();
            _monitors.Remove(acc.ProviderUID);

            logger.LogInformation("Stop monitoring for {email} with provider {prod}", acc.Email, acc.Provider);
        }
    }

    public void StartMonitor(Account acc)
    {
        if (!AppSettings.BackgroundMessageSync) return;


        lock (_lock)
        {
            if (_monitors.ContainsKey(acc.ProviderUID))
                return;

            var monitor = new AccountMonitor(acc.Provider);
            _monitors.Add(acc.ProviderUID, monitor);

            logger.LogInformation("Start monitoring for {email} with provider {prod}", acc.Email, acc.Provider);

            // SPAWN A THREAD (tiểu trình) to monitor
            // Hệ Điều Hành bài tiến trình =))
            _ = StartMonitorAsync(acc, monitor).ContinueWith(task =>
            {
                if (!task.IsFaulted) return;


                logger.LogError(task.Exception, "Cannot start monitoring for {email}", acc.Email);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBoxHelper.Error($"Không thể cập nhật trực tiếp cho tài khoản {acc.Email}, sử dụng nút refresh");
                });
            });
        }
    }

    private async Task StartMonitorAsync(Account acc, AccountMonitor monitor)
    {
        var cancellationToken = monitor.Cancellation.Token;

        try
        {
            // Different monitoring strategies based on provider
            switch (acc.Provider)
            {
                case Provider.Google:
                case Provider.Microsoft:
                    await MonitorWithPollingAsync(acc, cancellationToken);
                    break;
                default:
                    throw new NotImplementedException("Monitor implementation for this provider is not available");
            }


        }
        catch (Exception ex)
        {
            MessageBoxHelper.Error("Cannot start email realtime update: ", ex);
        }
        finally
        {
            lock (_lock)
            {
                if (_monitors.TryGetValue(acc.ProviderUID, out var existing) && existing == monitor)
                    _monitors.Remove(acc.ProviderUID);
            }
        }

    }

    private async Task MonitorWithPollingAsync(Account acc, CancellationToken cancellationToken)
    {
        // Adaptive polling intervals
        // THIS WILL GIVE ERROR WHEN OPEN WITH C# < 7
        int activeIntervalMs = AppSettings.AutoRefreshTime.Milliseconds; // default is 30 secs
        int idleIntervalMs = 120_000;     // per 2 minutes when idle
        int errorRetryIntervalMs = 60_000; // per 1 minute after error

        var currentInterval = activeIntervalMs;
        var consecutiveNoChanges = 0;

        var emailService = _emailServices.FirstOrDefault(x => x.GetProvider() == acc.Provider);
        if (emailService is null)
        {
            throw new NotImplementedException("Cannot find email service for this provider");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Store current email count
                var knownMessageIds = acc.Emails
                    .Select(e => e.MessageIdentifiers.ProviderMessageId)
                    .ToHashSet();

                // Fetch new emails using incremental sync
                await emailService.FetchEmailHeaderAsync(acc);

                // Find newly added emails
                var newEmails = acc.Emails
                    .Where(e => !knownMessageIds.Contains(e.MessageIdentifiers.ProviderMessageId))
                    .ToList();

                if (newEmails.Count > 0)
                {
                    // Reset to active polling
                    consecutiveNoChanges = 0;
                    currentInterval = activeIntervalMs;

                    // observable collection does not like it when we add email from different threads 
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        // Prefetch bodies for new emails
                        foreach (var email in newEmails)
                        {
                            await emailService.FetchEmailBodyAsync(acc, email);
                        }
                    });

                }
                else
                {
                    // if we there are no changes for 150 seconds, increase idle time
                    consecutiveNoChanges++;
                    if (consecutiveNoChanges > 5)
                    {
                        currentInterval = AppSettings.IncreasePollingTimeIfIdleForTooLong ? idleIntervalMs : AppSettings.AutoRefreshTime.Milliseconds;
                    }
                }

                // Wait before next poll
                await Task.Delay(currentInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break; // Normal exit
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBoxHelper.Error($"Polling error for {acc.Email}: {ex.Message}");
                });

                // Wait before retry
                await Task.Delay(errorRetryIntervalMs, cancellationToken);
            }
        }

    }


    private record AccountMonitor
    {
        public AccountMonitor(Provider provider)
        {
            Cancellation = new CancellationTokenSource();
            Provider = provider;
        }

        public CancellationTokenSource Cancellation { get; }
        public Provider Provider { get; }
    }
}