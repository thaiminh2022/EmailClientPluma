using System.Windows;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Storaging;
using MailKit.Net.Imap;

namespace EmailClientPluma.Core.Services.Emailing;

internal interface IEmailMonitoringService
{
    void StartMonitor(Account acc);
    void StopMonitor(Account acc);
}

internal class EmailMonitoringService : IEmailMonitoringService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, AccountMonitor> _monitors = [];
    private readonly List<IEmailService> _emailServices;

    public EmailMonitoringService(IEnumerable<IEmailService> emailServices)
    {
        _emailServices = [..emailServices];
    }

    public void StopMonitor(Account acc)
    {
        lock (_lock)
        {
            if (_monitors.TryGetValue(acc.ProviderUID, out var monitor))
            {
                monitor.Cancellation.Cancel();
                monitor.Cancellation.Dispose();
                _monitors.Remove(acc.ProviderUID);
            }
        }
    }

    public void StartMonitor(Account acc)
    {
        lock (_lock)
        {
            if (_monitors.ContainsKey(acc.ProviderUID))
                return;
            
            var monitor = new AccountMonitor(acc.Provider);
            _monitors.Add(acc.ProviderUID, monitor);

            // SPAWN A THREAD (tiểu trình) to monitor
            // Hệ Điều Hành bài tiến trình =))
            _ = StartMonitorAsync(acc, monitor).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBoxHelper.Error($"Monitoring failed for {acc.Email}: {task.Exception?.InnerException?.Message}");
                    });
                }
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
            const int ACTIVE_INTERVAL_MS = 30_000;    // 30 seconds when active
            const int IDLE_INTERVAL_MS = 120_000;     // 2 minutes when idle
            const int ERROR_RETRY_INTERVAL_MS = 60_000; // 1 minute after error

            var currentInterval = ACTIVE_INTERVAL_MS;
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
                        currentInterval = ACTIVE_INTERVAL_MS;

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
                            currentInterval = IDLE_INTERVAL_MS;
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
                    await Task.Delay(ERROR_RETRY_INTERVAL_MS, cancellationToken);
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