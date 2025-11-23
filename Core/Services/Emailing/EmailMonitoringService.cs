using EmailClientPluma.Core.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using System.Windows;

namespace EmailClientPluma.Core.Services.Emailing
{

    interface IEmailMonitoringService
    {
        void StartMonitor(Account acc);
        void StopMonitor(Account acc);
    }
    internal class EmailMonitoringService : IEmailMonitoringService
    {
        readonly IStorageService _storageService;

        readonly Dictionary<string, AccountMonitor> _monitors = [];
        readonly object _lock = new object();

        public EmailMonitoringService(IStorageService storageService)
        {
            _storageService = storageService;
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

                var knownEmailIds = acc.Emails.Select(e => e.MessageIdentifiers.ImapUID);
                var monitor = new AccountMonitor(knownEmailIds);
                _monitors.Add(acc.ProviderUID, monitor);

                // SPAWN A THREAD (tiểu trình) to monitor 
                // Hệ Điều Hành bài tiến trình =))
                _ = StartMonitorEmail(acc, monitor).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        // idk
                    }
                });
            }

        }

        async Task StartMonitorEmail(Account acc, AccountMonitor monitor)
        {
            CancellationToken cancellationToken = monitor.Cancellation.Token;

            try
            {
                // Reconnect loop
                // if anything failed, we wil try to reconnect
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Connect to the imap server
                        using var imap = new ImapClient();
                        await imap.ConnectAsync(
                            Helper.GetImapHostByProvider(acc.Provider),
                            993,
                            SecureSocketOptions.SslOnConnect,
                           cancellationToken
                        );

                        var supportsIdle = imap.Capabilities.HasFlag(ImapCapabilities.Idle);

                        // authenticate user
                        var oauth2 = new SaslMechanismOAuth2(acc.Email, acc.Credentials.SessionToken);
                        await imap.AuthenticateAsync(oauth2, cancellationToken);

                        // Connect to user INBOX  
                        var inbox = imap.Inbox;
                        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                        // Updating loop
                        while (!cancellationToken.IsCancellationRequested)
                        {

                            // THIS SECTION CONTAINS THE IDLING MECHANICS FOR CLIENT

                            // max wait time before PINGING the server
                            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(9));
                            bool inboxUpdated = false;
                            var inboxCount = inbox.Count;

                            void CountChanged(object? sender, EventArgs args)
                            {
                                inboxUpdated = true;
                                timeout.Cancel();
                            }

                            inbox.CountChanged += CountChanged;

                            try
                            {
                                using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);

                                if (supportsIdle)
                                {
                                    // THIS BLOCK WILL WAIT UNTIL TIMEOUT OR A CHANGE IN INBOX (NEW MESSAGE ARRIVE, MESSAGE DELETED)
                                    await imap.IdleAsync(linked.Token).ConfigureAwait(false);
                                }
                                else
                                {
                                    // Incase the server does not have the idle feature
                                    // PING it manually every 9 minutes
                                    await Task.Delay(TimeSpan.FromMinutes(9), linked.Token).ConfigureAwait(false);
                                    await imap.NoOpAsync(linked.Token).ConfigureAwait(false);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // user may delete their account while monitoring, this will ensure we exit properly
                            }
                            finally
                            {
                                // either something changes or timeouted, so we dont need to track it any more
                                inbox.CountChanged -= CountChanged;
                            }

                            if (cancellationToken.IsCancellationRequested)
                                break;


                            // nothing changes, it's the imap PINGING the server
                            if (!inboxUpdated)
                                continue;

                            // SOMETHING CHANGES

                            var newInboxCount = inbox.Count;
                            if (newInboxCount < inboxCount)
                            {
                                // Message was delete, we are not handling this now, so ignore lol
                                continue;
                            }

                            var take = newInboxCount - inboxCount;
                            var fetchCount = Math.Min(take, newInboxCount);

                            if (fetchCount <= 0)
                                continue;

                            // fetch the headers
                            var start = Math.Max(0, newInboxCount - fetchCount);
                            var summaries = await inbox.FetchAsync(start, newInboxCount - 1,
                                MessageSummaryItems.Envelope |
                                MessageSummaryItems.UniqueId,
                                cancellationToken
                            );

                            // Rebuilding the email from summaries
                            List<Email> emails = [];
                            foreach (var item in summaries)
                            {
                                var uniqueId = item.UniqueId.Id;

                                // If the message already existed then ignore
                                if (!monitor.KnownUids.Add(uniqueId))
                                {
                                    continue;
                                }

                                //fetch the full body
                                var email = Helper.CreateEmailFromSummary(acc, inbox, item);
                                var message = await inbox.GetMessageAsync(item.UniqueId, cancellationToken);
                                email.MessageParts.Body = message.HtmlBody;

                                // Store the newly fetched email
                                await _storageService.StoreEmailAsync(acc, email);

                                // Does not support UI change from a different thread, so we calling the original thread
                                emails.Add(email);

                                // No point of doing this, since if we reconnect, we gonna recall ids anyway
                                // just put this here so i dont forget why we should not do this

                                //monitor.KnownUids.Add(email.MessageIdentifiers.ImapUID);
                            }

                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                foreach (var email in emails)
                                {
                                    acc.Emails.Add(email);
                                }
                            });

                        }

                        // Disconnect and be ready for the next reconnect
                        await imap.DisconnectAsync(true);
                    }
                    catch (OperationCanceledException)
                    {
                        // Normal exit
                        break;
                    }
                    catch (Exception ex)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBoxHelper.Error($"Problem with monitoring email: {ex.Message}");
                        });

                        // Wait 10 sec to retry connect
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }
                }
            }
            finally
            {
                // Exit the tracking, meaning someone finally called stop
                lock (_lock)
                {
                    // Even tho we already remove the monitor at stopMonitor, but this ensure some bullshit thread race condition wont happen
                    if (_monitors.TryGetValue(acc.ProviderUID, out var existing) && existing == monitor)
                        _monitors.Remove(acc.ProviderUID);
                }
            }
        }



        record AccountMonitor
        {
            public CancellationTokenSource Cancellation { get; }
            public HashSet<uint> KnownUids { get; }

            public AccountMonitor(IEnumerable<uint> knownUids)
            {
                Cancellation = new CancellationTokenSource();
                KnownUids = [.. knownUids];
            }
        }
    }
}
