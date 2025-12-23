using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Models.Exceptions;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Storaging;
using Microsoft.Graph;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Newtonsoft.Json;

namespace EmailClientPluma.Core.Services.Emailing;

internal class OutlookApiEmailService : IEmailService
{
    private const int INITIAL_HEADER_WINDOW = 20;
    private readonly IStorageService _storageService;
    private readonly IMicrosoftClientApp _clientApp;

    public OutlookApiEmailService(IMicrosoftClientApp clientApp, IStorageService storageService)
    {
        _clientApp = clientApp;
        _storageService = storageService;
    }

    private GraphServiceClient GetGraphService(Account acc)
    {
        var provider = new MsalAccessTokenProvider(_clientApp.PublicClient, _clientApp.Scopes, acc.ProviderUID);
        var tokenProvider = new BaseBearerTokenAuthenticationProvider(provider);
        return new GraphServiceClient(tokenProvider);
    }

    private (PaginationTok?, LastSyncTok?) ParseAccountTokens(string? pagTok, string? lastTok)
    {
        PaginationTok? pag = null;
        LastSyncTok? last = null;

        if (pagTok is not null)
        {
            pag = JsonConvert.DeserializeObject<PaginationTok>(pagTok);
        }

        if (lastTok is not null)
        {
            last = JsonConvert.DeserializeObject<LastSyncTok>(lastTok);
        }

        return (pag, last);
    }

    public async Task FetchEmailHeaderAsync(Account acc)
    {
        var graphClient = GetGraphService(acc);
        var (_, lastSync) = ParseAccountTokens(acc.PaginationToken, acc.LastSyncToken);


        try
        {
            if (lastSync is not null)
            {
                // incremental
                await FetchIncremental(acc, graphClient, lastSync.Value);
            }
            else
            {
                // initial
                await FetchInitial(acc, graphClient);
            }


            acc.NoMoreOlderEmail = string.IsNullOrEmpty(acc.PaginationToken);
            await _storageService.UpdatePaginationAndNextTokenAsync(acc);
        }
        catch (Exception ex)
        {
            throw new EmailFetchException(inner: ex);
        }
    }

    private async Task FetchInitial(Account acc, GraphServiceClient graphClient)
    {
        var inboxFolder = await graphClient.Me.MailFolders["inbox"].GetAsync();
        var sentFolder = await graphClient.Me.MailFolders["sentitems"].GetAsync();

        if (inboxFolder?.Id == null || sentFolder?.Id == null)
        {
            throw new Exception("Cannot access Inbox or Sent folder");
        }

        var deltaInbox = graphClient.Me.MailFolders[inboxFolder.Id].Messages.Delta;
        var deltaSent = graphClient.Me.MailFolders[sentFolder.Id].Messages.Delta;


        var deltaResponseInbox = await deltaInbox.GetAsDeltaGetResponseAsync(config =>
        {
            config.QueryParameters.Select =
            [
                "id", "subject", "from", "toRecipients",
                "isRead", "receivedDateTime", "conversationId",
                "internetMessageId", "internetMessageHeaders", "isDraft"
            ];
            config.QueryParameters.Top = INITIAL_HEADER_WINDOW;
            config.QueryParameters.Orderby = ["receivedDateTime DESC"];
        });


        var deltaResponseSent = await deltaSent.GetAsDeltaGetResponseAsync(config =>
        {
            config.QueryParameters.Select =
            [
                "id", "subject", "from", "toRecipients",
                "isRead", "receivedDateTime", "conversationId",
                "internetMessageId", "internetMessageHeaders", "isDraft"
            ];
            config.QueryParameters.Top = INITIAL_HEADER_WINDOW;
            config.QueryParameters.Orderby = ["receivedDateTime DESC"];
        });



        if (deltaResponseInbox?.Value is not null)
        {
            foreach (var msg in deltaResponseInbox.Value)
            {
                var email = CreateEmailFromGraph(acc, msg);

                if (acc.Emails.Any(x =>
                        x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
                    continue;

                acc.Emails.Add(email);
                await _storageService.StoreEmailAsync(acc, email);
            }
        }

        if (deltaResponseSent?.Value is not null)
        {
            foreach (var msg in deltaResponseSent.Value)
            {
                var email = CreateEmailFromGraph(acc, msg, true);

                if (acc.Emails.Any(x =>
                        x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
                    continue;

                acc.Emails.Add(email);
                await _storageService.StoreEmailAsync(acc, email);
            }
        }

        var lastSyncTok = JsonConvert.SerializeObject(new LastSyncTok
        {
            InboxDeltaLink = deltaResponseInbox?.OdataDeltaLink,
            SentDeltaLink = deltaResponseSent?.OdataDeltaLink
        });

        var paginationTok = JsonConvert.SerializeObject(new PaginationTok
        {
            InboxNextLink = deltaResponseInbox?.OdataNextLink,
            SentNextLink = deltaResponseSent?.OdataNextLink
        });

        acc.LastSyncToken = lastSyncTok;
        acc.PaginationToken = paginationTok;

        if (deltaResponseInbox?.OdataNextLink is null && deltaResponseSent?.OdataNextLink is null)
        {
            acc.NoMoreOlderEmail = true;
        }
    }

    struct PaginationTok
    {
        public string? InboxNextLink;
        public string? SentNextLink;
    }

    struct LastSyncTok
    {
        public string? InboxDeltaLink;
        public string? SentDeltaLink;
    }

    private async Task FetchIncremental(Account acc, GraphServiceClient graphClient, LastSyncTok lastTok)
    {
        var deltaResponseInbox = await graphClient.Me.MailFolders["inbox"].Messages.Delta
            .WithUrl(lastTok.InboxDeltaLink)
            .GetAsDeltaGetResponseAsync();

        var deltaResponseSent = await graphClient.Me.MailFolders["sentitems"].Messages.Delta
            .WithUrl(lastTok.SentDeltaLink)
            .GetAsDeltaGetResponseAsync();

        if (deltaResponseInbox?.Value is not null)
        {
            foreach (var msg in deltaResponseInbox.Value)
            {

                // New or updated message
                var existingEmail = acc.Emails.FirstOrDefault(e =>
                    e.MessageIdentifiers.ProviderMessageId == msg.Id);

                // only handle new messages
                if (existingEmail is not null) continue;

                // New email
                var newEmail = CreateEmailFromGraph(acc, msg);
                acc.Emails.Add(newEmail);
                await _storageService.StoreEmailAsync(acc, newEmail);
            }

        }

        if (deltaResponseSent?.Value is not null)
        {
            foreach (var msg in deltaResponseSent.Value)
            {

                // New or updated message
                var existingEmail = acc.Emails.FirstOrDefault(e =>
                    e.MessageIdentifiers.ProviderMessageId == msg.Id);

                // only handle new messages
                if (existingEmail is not null) continue;

                // New email
                var newEmail = CreateEmailFromGraph(acc, msg, true);
                acc.Emails.Add(newEmail);
                await _storageService.StoreEmailAsync(acc, newEmail);
            }

        }

        var lastSyncTok = JsonConvert.SerializeObject(new LastSyncTok
        {
            InboxDeltaLink = deltaResponseInbox?.OdataDeltaLink,
            SentDeltaLink = deltaResponseSent?.OdataDeltaLink
        });

        var paginationTok = JsonConvert.SerializeObject(new PaginationTok
        {
            InboxNextLink = deltaResponseInbox?.OdataNextLink,
            SentNextLink = deltaResponseSent?.OdataNextLink
        });

        acc.LastSyncToken = lastSyncTok;
        acc.PaginationToken = paginationTok;
    }

    private static string? GetHeader(Message msg, string headerName)
    {
        return msg.InternetMessageHeaders?
            .FirstOrDefault(h => string.Equals(h.Name, headerName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private Email CreateEmailFromGraph(Account acc, Message msg, bool fromSent = false)
    {
        // Flags (best-effort mapping; Graph doesn't have Gmail-like labels)
        var flags = EmailFlags.None;

        if (fromSent)
        {
            flags |= EmailFlags.Sent;
        }

        if (msg.IsRead == true)
            flags |= EmailFlags.Seen;

        // Optional: mark drafts if available (Graph Message has IsDraft in many SDK versions)
        if (msg.IsDraft == true)
            flags |= EmailFlags.Draft;

        // NOTE: "Sent" is usually inferred by folder (Sent Items) rather than a flag on Message.
        // Since you are fetching from Inbox, we keep Sent off.

        var identifiers = new Email.Identifiers
        {
            OwnerAccountId = acc.ProviderUID,
            ProviderMessageId = msg.Id!, // Graph message id
            ProviderThreadId = msg.ConversationId,
            InternetMessageId = msg.InternetMessageId,
            InReplyTo = GetHeader(msg, "In-Reply-To"),
            ProviderHistoryId = null, // Graph doesn't expose Gmail-like HistoryId
            FolderFullName = fromSent ? "Sent Items" : "Inbox",
            Provider = Provider.Microsoft,
            Flags = flags,
        };
        var fromAddr = msg.From?.EmailAddress?.Address ?? string.Empty;

        var toAddr = (msg.ToRecipients ?? new List<Recipient>())
            .Select(r => r.EmailAddress?.Address)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

        var data = new Email.DataParts
        {
            Subject = msg.Subject ?? string.Empty,
            Body = null, // header fetch: don't fetch body here
            From = fromAddr,
            To = string.Join(", ", toAddr),
            Date = msg.ReceivedDateTime, // DateTimeOffset?
            EmailSizeInKb = 0
        };

        var e = new Email(identifiers, data);

        // Labels: you can tune these to match your UI semantics
        e.Labels.Add(EmailLabel.All);
        e.Labels.Add(fromSent ? EmailLabel.Sent : EmailLabel.Inbox);

        return e;
    }

    public async Task FetchEmailBodyAsync(Account acc, Email email)
    {
        var client = GetGraphService(acc);

        // Get specific message with body
        try
        {
            var msg = await client.Me.Messages[email.MessageIdentifiers.ProviderMessageId]
                .GetAsync(config => { config.QueryParameters.Select = ["body"]; });

            email.MessageParts.Body = msg?.Body?.Content ?? "(No Body)";
        }
        catch (Exception ex)
        {
            throw new EmailFetchException(inner: ex);
        }
    }

    public async Task SendEmailAsync(Account acc, Email.OutgoingEmail email)
    {
        var fromRecipient = new Recipient()
        {
            EmailAddress = new EmailAddress()
            {
                Address = email.From,
                Name = acc.DisplayName
            }

        };

        var toRecipients = email.To.Split(",").Select(mail => new Recipient()
        {
            EmailAddress = new EmailAddress()
            {
                Address = mail
            }
        }).ToList();


        List<Recipient> replyToRecipients = [];

        if (email.ReplyTo is not null)
        {
            replyToRecipients.Add(new Recipient()
            {
                EmailAddress = new EmailAddress()
                {
                    Address = email.ReplyTo
                }
            });
        }

        var message = new Message
        {
            Subject = email.Subject,
            Body = new ItemBody()
            {
                ContentType = BodyType.Html,
                Content = email.Body,
            },
            From = fromRecipient,
            ToRecipients = toRecipients,
            ReplyTo = replyToRecipients,
            SentDateTime = email.Date
        };

        var client = GetGraphService(acc);
        try
        {
            await client.Me.SendMail.PostAsync(new SendMailPostRequestBody()
            {
                Message = message,
                SaveToSentItems = true,
            });
        }
        catch (Exception ex)
        {
            throw new EmailSendException(inner: ex);
        }
  
    }

    public async Task PrefetchRecentBodiesAsync(Account acc, int maxToPrefetch = 30)
    {
        var candidates = acc.Emails
            .Where(e => !e.BodyFetched)
            .OrderByDescending(e => e.MessageParts.Date)
            .Take(maxToPrefetch)
            .ToList();

        if (candidates.Count == 0)
            return;

        var client = GetGraphService(acc);

        foreach (var candidate in candidates)
        {
            try
            {
                var msg = await client.Me.Messages[candidate.MessageIdentifiers.ProviderMessageId]
                    .GetAsync(cfg =>
                    {
                        cfg.QueryParameters.Select = ["body"];
                    });

                candidate.MessageParts.Body = msg?.Body?.Content ?? "(No Body)";
                await _storageService.UpdateEmailBodyAsync(candidate);
            }
            catch (Exception ex)
            {
                // throw new EmailFetchException(inner: ex);
                // ignore, for logging only
            }
        }
    }

    public async Task<bool> FetchOlderHeadersAsync(Account acc, int window, CancellationToken token = default)
    {
        if (acc.NoMoreOlderEmail)
            return false;

        var (pag, _) = ParseAccountTokens(acc.PaginationToken, acc.LastSyncToken);
        if (pag is null)
            return false;

        var client = GetGraphService(acc);

        var pagTok = new PaginationTok();

        if (pag.Value.InboxNextLink is not null)
        {
            // inbox
            MessageCollectionResponse? pageInbox;
            try
            {
                pageInbox = await client.Me.MailFolders["inbox"].Messages
                    .WithUrl(pag.Value.InboxNextLink)
                    .GetAsync(cancellationToken: token);
            }
            catch (Exception ex)
            {
                throw new EmailTokenException(inner: ex);
            }

            if (pageInbox?.Value is null || pageInbox.Value.Count == 0)
            {
                acc.NoMoreOlderEmail = true;
                return false;
            }

            foreach (var msg in pageInbox.Value)
            {
                var email = CreateEmailFromGraph(acc, msg);

                if (acc.Emails.Any(x => x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
                    continue;

                acc.Emails.Add(email);
                await _storageService.StoreEmailAsync(acc, email);
            }

            pagTok.InboxNextLink = pageInbox.OdataNextLink;
        }


        if (pag.Value.SentNextLink is not null)
        {

            // sent
            MessageCollectionResponse? pageSent;
            try
            {
                pageSent = await client.Me.MailFolders["sentitems"].Messages
                    .WithUrl(pag.Value.SentNextLink)
                    .GetAsync(cancellationToken: token);
            }
            catch (Exception ex)
            {
                throw new EmailTokenException(inner: ex);
            }
       

            if (pageSent?.Value is not null && pageSent.Value.Count != 0)
            {
                foreach (var msg in pageSent.Value)
                {
                    var email = CreateEmailFromGraph(acc, msg, true);

                    if (acc.Emails.Any(x => x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
                        continue;

                    acc.Emails.Add(email);
                    await _storageService.StoreEmailAsync(acc, email);
                }
            }

            pagTok.SentNextLink = pageSent?.OdataNextLink;
        }


        acc.PaginationToken = JsonConvert.SerializeObject(pagTok);
        acc.NoMoreOlderEmail = string.IsNullOrEmpty(pagTok.InboxNextLink) && string.IsNullOrEmpty(pagTok.SentNextLink);

        await _storageService.UpdatePaginationAndNextTokenAsync(acc);
        return true;
    }

    public Provider GetProvider()
    {
        return Provider.Microsoft;
    }
}