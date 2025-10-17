using EmailClientPluma.Core.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EmailClientPluma.Core.Services
{
    interface IEmailService
    {
        Task FetchEmailHeaderAsync(Account acc);
        Task FetchEmailBodyAsync(Account acc, Email email);
        Task SendEmailAsync(Account acc, Email.OutgoingEmail email);
        void StartRealtimeUpdates(Account acc);
        void StopRealtimeUpdates(Account acc);
        event EventHandler<EmailReceivedEventArgs>? EmailReceived;
    }

    internal class EmailService : IEmailService
    {
        readonly IStorageService _storageService;
        readonly Dictionary<string, AccountMonitor> _monitors = [];
        readonly object _monitorLock = new();

        public EmailService(IStorageService storageService)
        {
            _storageService = storageService;
        }

        public event EventHandler<EmailReceivedEventArgs>? EmailReceived;

        public async Task FetchEmailHeaderAsync(Account acc)
        {
            using var imap = new ImapClient();
            await imap.ConnectAsync(GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect);
            var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
            await imap.AuthenticateAsync(oauth2);

            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            if (inbox.Count == 0)
            {
                await imap.DisconnectAsync(true);
                return;
            }

            int take = Math.Min(20, inbox.Count);
            int start = Math.Max(0, inbox.Count - take);

            var summaries = await inbox.FetchAsync(start, inbox.Count - 1, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);

            foreach (var item in summaries)
            {
                if (acc.Emails.Any(existing => existing.MessageIdentifiers.ImapUID == item.UniqueId.Id))
                {
                    continue;
                }

                var email = CreateEmailFromSummary(acc, inbox, item);
                acc.Emails.Insert(0, email);
                RegisterKnownEmail(acc, email);
                await _storageService.StoreEmailAsync(acc, email);
            }

            await imap.DisconnectAsync(true);
        }

        public async Task FetchEmailBodyAsync(Account acc, Email email)
        {
            using var imap = new ImapClient();
            await imap.ConnectAsync(GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect);
            var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
            await imap.AuthenticateAsync(oauth2);

            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            var uniqueID = new UniqueId(email.MessageIdentifiers.ImapUID);

            try
            {
                var bodies = await inbox.FetchAsync([uniqueID], MessageSummaryItems.BodyStructure);
                var bodyParts = bodies?.FirstOrDefault();
                if (bodies is null || bodyParts is null)
                {
                    email.MessageParts.Body = "(Unable to fetch body)";
                    return;
                }

                var chosen = bodyParts.HtmlBody ?? bodyParts.TextBody;
                var entity = await inbox.GetBodyPartAsync(uniqueID, chosen);
                if (entity is TextPart textPart)
                {
                    email.MessageParts.Body = textPart.Text;
                }
                else
                {
                    email.MessageParts.Body = "(No Body)";
                }
                await _storageService.UpdateEmailBodyAsync(email);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public async Task SendEmailAsync(Account acc, Email.OutgoingEmail email)
        {
            var message = ConstructEmail(acc, email);
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(GetSmtpHostByProvider(acc.Provider), 587, SecureSocketOptions.StartTls);
            var oauth2 = new SaslMechanismOAuth2(acc.Email, acc.Credentials.SessionToken);
            await smtp.AuthenticateAsync(oauth2);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        public void StartRealtimeUpdates(Account acc)
        {
            lock (_monitorLock)
            {
                if (_monitors.ContainsKey(acc.ProviderUID))
                {
                    return;
                }

                var knownUids = acc.Emails.Select(email => email.MessageIdentifiers.ImapUID);
                var monitor = new AccountMonitor(knownUids);
                _monitors.Add(acc.ProviderUID, monitor);
                _ = MonitorInboxAsync(acc, monitor, monitor.Cancellation.Token);
            }
        }

        public void StopRealtimeUpdates(Account acc)
        {
            lock (_monitorLock)
            {
                if (_monitors.TryGetValue(acc.ProviderUID, out var monitor))
                {
                    monitor.Cancellation.Cancel();
                    monitor.Cancellation.Dispose();
                    _monitors.Remove(acc.ProviderUID);
                }
            }
        }

        async Task MonitorInboxAsync(Account acc, AccountMonitor monitor, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        using var imap = new ImapClient();
                        await imap.ConnectAsync(GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect, cancellationToken);
                        var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
                        await imap.AuthenticateAsync(oauth2, cancellationToken);

                        var inbox = imap.Inbox;
                        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            using var done = new CancellationTokenSource(TimeSpan.FromMinutes(9));
                            var newMessageArrived = false;

                            void CountChangedHandler(object? sender, EventArgs e)
                            {
                                newMessageArrived = true;
                                done.Cancel();
                            }

                            inbox.CountChanged += CountChangedHandler;

                            try
                            {
                                using var linked = CancellationTokenSource.CreateLinkedTokenSource(done.Token, cancellationToken);
                                await inbox.IdleAsync(linked.Token);
                            }
                            catch (OperationCanceledException)
                            {
                            }
                            finally
                            {
                                inbox.CountChanged -= CountChangedHandler;
                            }

                            if (cancellationToken.IsCancellationRequested)
                                break;

                            if (!newMessageArrived)
                                continue;

                            var fetchCount = Math.Min(5, inbox.Count);
                            if (fetchCount <= 0)
                                continue;

                            var start = Math.Max(0, inbox.Count - fetchCount);
                            var summaries = await inbox.FetchAsync(start, inbox.Count - 1, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId, cancellationToken);

                            foreach (var item in summaries)
                            {
                                var uniqueId = item.UniqueId.Id;
                                if (!monitor.KnownUids.Add(uniqueId))
                                {
                                    continue;
                                }

                                var email = CreateEmailFromSummary(acc, inbox, item);
                                await _storageService.StoreEmailAsync(acc, email);
                                EmailReceived?.Invoke(this, new EmailReceivedEventArgs(acc, email));
                            }
                        }

                        await imap.DisconnectAsync(true);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        var dispatcher = Application.Current?.Dispatcher;
                        if (dispatcher is not null && !dispatcher.CheckAccess())
                        {
                            dispatcher.Invoke(() => MessageBox.Show(ex.Message));
                        }
                        else
                        {
                            MessageBox.Show(ex.Message);
                        }
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }
                }
            }
            finally
            {
                lock (_monitorLock)
                {
                    if (_monitors.TryGetValue(acc.ProviderUID, out var existing) && existing == monitor)
                    {
                        _monitors.Remove(acc.ProviderUID);
                    }
                }
            }
        }

        void RegisterKnownEmail(Account acc, Email email)
        {
            lock (_monitorLock)
            {
                if (_monitors.TryGetValue(acc.ProviderUID, out var monitor))
                {
                    monitor.KnownUids.Add(email.MessageIdentifiers.ImapUID);
                }
            }
        }

        static Email CreateEmailFromSummary(Account acc, IMailFolder inbox, IMessageSummary summary)
        {
            var env = summary.Envelope;
            var uniqueID = summary.UniqueId.Id;
            var uidValidity = inbox.UidValidity;

            return new Email(
                new Email.Identifiers
                {
                    ImapUID = uniqueID,
                    ImapUIDValidity = uidValidity,
                    FolderFullName = inbox.FullName,
                    MessageID = env.MessageId,
                    OwnerAccountID = acc.ProviderUID,
                    InReplyTo = env.InReplyTo,

                },
                new Email.DataParts
                {
                    Subject = env.Subject ?? "(No Subject)",
                    From = env.From.ToString(),
                    To = env.To.ToString(),
                    Date = env.Date
                }
            );
        }

        static MimeMessage ConstructEmail(Account acc, Email.OutgoingEmail email)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(acc.Email));

            InternetAddressList internetAddresses = [];
            foreach (var item in email.To.Split(','))
            {
                internetAddresses.Add(InternetAddress.Parse(item));
            }
            message.To.AddRange(internetAddresses);

            message.Subject = email.Subject;
            if (email.ReplyTo != null)
            {
                message.ReplyTo.Add(MailboxAddress.Parse(email.ReplyTo));
            }

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = email.Body
            };

            foreach (var item in email.Attachments)
            {
                bodyBuilder.Attachments.Add(item.FileName, item.Content);
            }
            message.Body = bodyBuilder.ToMessageBody();

            message.Date = email.Date ?? DateTimeOffset.Now;
            return message;
        }

        static string GetSmtpHostByProvider(Provider prod)
        {
            return prod switch
            {
                Provider.Google => "smtp.gmail.com",
                _ => throw new NotImplementedException()
            };
        }

        static string GetImapHostByProvider(Provider prod)
        {
            return prod switch
            {
                Provider.Google => "imap.gmail.com",
                _ => throw new NotImplementedException()
            };
        }

        class AccountMonitor
        {
            public AccountMonitor(IEnumerable<uint> knownUids)
            {
                Cancellation = new CancellationTokenSource();
                KnownUids = new HashSet<uint>(knownUids);
            }

            public CancellationTokenSource Cancellation { get; }
            public HashSet<uint> KnownUids { get; }
        }
    }

    internal class EmailReceivedEventArgs : EventArgs
    {
        public EmailReceivedEventArgs(Account account, Email email)
        {
            Account = account;
            Email = email;
        }

        public Account Account { get; }
        public Email Email { get; }
    }
}
