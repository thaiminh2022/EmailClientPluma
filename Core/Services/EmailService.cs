using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EmailClientPluma.Core.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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

    internal class EmailService : IEmailService
    {
        private readonly IStorageService _storageService;
        private readonly Dictionary<string, CancellationTokenSource> _realtimeTokens = new();
        private readonly Dictionary<string, HashSet<uint>> _knownUids = new();
        private readonly object _monitoringLock = new();
        private readonly object _knownUidLock = new();

        public EmailService(IStorageService storageService)
        {
            _storageService = storageService;
        }

        public event EventHandler<EmailReceivedEventArgs>? EmailReceived;

        public async Task FetchEmailHeaderAsync(Account acc)
        {
            InitializeKnownUidSet(acc);

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

            List<Email> newlyFetched = new();
            foreach (var summary in summaries)
            {
                if (!summary.UniqueId.IsValid)
                {
                    continue;
                }

                if (!TryTrackUid(acc, summary.UniqueId.Id))
                {
                    continue;
                }

                var email = CreateEmailFromSummary(acc, inbox, summary);
                newlyFetched.Add(email);
            }

            foreach (var email in newlyFetched.OrderBy(e => e.MessageIdentifiers.ImapUID))
            {
                acc.Emails.Add(email);
            }

            if (newlyFetched.Count > 0)
            {
                await _storageService.StoreEmailsAsync(acc, newlyFetched);
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
                var bodies = await inbox.FetchAsync(new[] { uniqueID }, MessageSummaryItems.BodyStructure);
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
            InitializeKnownUidSet(acc);

            CancellationTokenSource? cts = null;
            lock (_monitoringLock)
            {
                if (_realtimeTokens.ContainsKey(acc.ProviderUID))
                {
                    return;
                }

                cts = new CancellationTokenSource();
                _realtimeTokens[acc.ProviderUID] = cts;
            }

            if (cts is null)
            {
                return;
            }

            _ = Task.Run(() => MonitorAccountAsync(acc, cts.Token));
        }

        public void StopRealtimeUpdates(Account acc)
        {
            CancellationTokenSource? cts = null;
            lock (_monitoringLock)
            {
                if (_realtimeTokens.TryGetValue(acc.ProviderUID, out var existing))
                {
                    cts = existing;
                    _realtimeTokens.Remove(acc.ProviderUID);
                }
            }

            cts?.Cancel();
            cts?.Dispose();

            lock (_knownUidLock)
            {
                _knownUids.Remove(acc.ProviderUID);
            }
        }

        private async Task MonitorAccountAsync(Account acc, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var imap = new ImapClient();
                    await imap.ConnectAsync(GetImapHostByProvider(acc.Provider), 993, SecureSocketOptions.SslOnConnect, token);
                    var oauth2 = new SaslMechanismOAuth2(new(acc.Email, acc.Credentials.SessionToken));
                    await imap.AuthenticateAsync(oauth2, token);

                    var inbox = imap.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadOnly, token);

                    await FetchNewMessagesAsync(acc, inbox, token);

                    while (!token.IsCancellationRequested)
                    {
                        using var done = new CancellationTokenSource(TimeSpan.FromMinutes(9));
                        var messageArrived = false;

                        void OnMessagesArrived(object? sender, MessagesArrivedEventArgs e)
                        {
                            messageArrived = true;
                            done.Cancel();
                        }

                        inbox.MessagesArrived += OnMessagesArrived;

                        try
                        {
                            await imap.IdleAsync(done.Token, token);
                        }
                        catch (OperationCanceledException) when (!token.IsCancellationRequested)
                        {
                            // Idle cancelled due to timeout or new messages.
                        }
                        finally
                        {
                            inbox.MessagesArrived -= OnMessagesArrived;
                        }

                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        await FetchNewMessagesAsync(acc, inbox, token);

                        if (!messageArrived)
                        {
                            continue;
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task FetchNewMessagesAsync(Account acc, IMailFolder inbox, CancellationToken token)
        {
            if (inbox.Count == 0)
            {
                return;
            }

            int take = Math.Min(20, inbox.Count);
            int start = Math.Max(0, inbox.Count - take);
            var summaries = await inbox.FetchAsync(start, inbox.Count - 1, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId, token);

            List<Email> newEmails = new();
            foreach (var summary in summaries)
            {
                if (!summary.UniqueId.IsValid)
                {
                    continue;
                }

                if (!TryTrackUid(acc, summary.UniqueId.Id))
                {
                    continue;
                }

                var email = CreateEmailFromSummary(acc, inbox, summary);
                newEmails.Add(email);
            }

            if (newEmails.Count == 0)
            {
                return;
            }

            await _storageService.StoreEmailsAsync(acc, newEmails);

            foreach (var email in newEmails.OrderBy(e => e.MessageIdentifiers.ImapUID))
            {
                EmailReceived?.Invoke(this, new EmailReceivedEventArgs(acc, email));
            }
        }

        private bool TryTrackUid(Account acc, uint uid)
        {
            lock (_knownUidLock)
            {
                if (!_knownUids.TryGetValue(acc.ProviderUID, out var set))
                {
                    set = new HashSet<uint>();
                    _knownUids[acc.ProviderUID] = set;
                }

                return set.Add(uid);
            }
        }

        private void InitializeKnownUidSet(Account acc)
        {
            lock (_knownUidLock)
            {
                if (_knownUids.ContainsKey(acc.ProviderUID))
                {
                    return;
                }

                var existing = acc.Emails.Select(e => e.MessageIdentifiers.ImapUID);
                _knownUids[acc.ProviderUID] = new HashSet<uint>(existing);
            }
        }

        private static Email CreateEmailFromSummary(Account acc, IMailFolder inbox, IMessageSummary summary)
        {
            var env = summary.Envelope;
            return new Email(
                new Email.Identifiers
                {
                    ImapUID = summary.UniqueId.Id,
                    ImapUIDValidity = inbox.UidValidity,
                    FolderFullName = inbox.FullName,
                    MessageID = env?.MessageId,
                    OwnerAccountID = acc.ProviderUID,
                    InReplyTo = env?.InReplyTo,
                },
                new Email.DataParts
                {
                    Subject = env?.Subject ?? "(No Subject)",
                    From = env?.From?.ToString() ?? string.Empty,
                    To = env?.To?.ToString() ?? string.Empty,
                    Date = env?.Date
                }
            );
        }

        private static MimeMessage ConstructEmail(Account acc, Email.OutgoingEmail email)
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

        private static string GetSmtpHostByProvider(Provider prod)
        {
            return prod switch
            {
                Provider.Google => "smtp.gmail.com",
                _ => throw new NotImplementedException()
            };
        }
        private static string GetImapHostByProvider(Provider prod)
        {
            return prod switch
            {
                Provider.Google => "imap.gmail.com",
                _ => throw new NotImplementedException()
            };
        }


    }
}
