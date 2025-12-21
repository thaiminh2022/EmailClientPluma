using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Storaging;
using Microsoft.Graph;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using DeltaGetResponse = Microsoft.Graph.Me.MailFolders.Item.Messages.Delta.DeltaGetResponse;

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

    public async Task FetchEmailHeaderAsync(Account acc)
    {
        var graphClient = GetGraphService(acc);

        try
        {
            DeltaGetResponse? deltaResponse;
            if (!string.IsNullOrEmpty(acc.LastSyncToken))
            {
                deltaResponse = await graphClient.Me.MailFolders["Inbox"].Messages.Delta
                    .WithUrl(acc.LastSyncToken)
                    .GetAsDeltaGetResponseAsync();

                if (deltaResponse?.Value is not null)
                {
                    foreach (var msg in deltaResponse.Value)
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
            }
            else
            {
                // initial
                var inboxFolder = await graphClient.Me.MailFolders["Inbox"].GetAsync();

                if (inboxFolder?.Id == null)
                {
                    throw new Exception("Cannot access Inbox folder");
                }

                var deltaRequest = graphClient.Me.MailFolders[inboxFolder.Id].Messages.Delta;

                deltaResponse = await deltaRequest.GetAsDeltaGetResponseAsync(config =>
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
                if (deltaResponse?.Value != null)
                {
                    foreach (var msg in deltaResponse.Value)
                    {
                        var email = CreateEmailFromGraph(acc, msg);

                        if (acc.Emails.Any(x =>
                                x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
                            continue;

                        acc.Emails.Add(email);
                        await _storageService.StoreEmailAsync(acc, email);
                    }
                }
            }

            // Store the delta link for incremental sync
            if (!string.IsNullOrEmpty(deltaResponse?.OdataDeltaLink))
            {
                acc.LastSyncToken = deltaResponse.OdataDeltaLink;
            }

            if (!string.IsNullOrEmpty(deltaResponse?.OdataNextLink))
            {
                acc.PaginationToken = deltaResponse.OdataNextLink;
            }

            acc.NoMoreOlderEmail = string.IsNullOrEmpty(acc.PaginationToken);
            await _storageService.UpdatePaginationAndNextTokenAsync(acc);
        }
        catch (Exception)
        {
            MessageBoxHelper.Error("Cannot fetch your account message");
        }
    }

    private static string? GetHeader(Message msg, string headerName)
    {
        return msg.InternetMessageHeaders?
            .FirstOrDefault(h => string.Equals(h.Name, headerName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private Email CreateEmailFromGraph(Account acc, Message msg)
    {
        // Flags (best-effort mapping; Graph doesn't have Gmail-like labels)
        var flags = EmailFlags.None;

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
            FolderFullName = "Inbox",
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
        e.Labels.Add(EmailLabel.Inbox);

        return e;
    }

    public async Task FetchEmailBodyAsync(Account acc, Email email)
    {
        var client = GetGraphService(acc);

        // Get specific message with body
        var msg = await client.Me.Messages[email.MessageIdentifiers.ProviderMessageId]
            .GetAsync(config => { config.QueryParameters.Select = ["body"]; });

        email.MessageParts.Body = msg?.Body?.Content ?? "(No Body)";
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
        await client.Me.SendMail.PostAsync(new SendMailPostRequestBody()
        {
            Message = message,
            SaveToSentItems = true,
        });
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
                // keep going for others
                MessageBoxHelper.Error(ex.Message);
            }
        }
    }

    public async Task<bool> FetchOlderHeadersAsync(Account acc, int window, CancellationToken token = default)
    {
        if (acc.NoMoreOlderEmail)
            return false;

        if (string.IsNullOrEmpty(acc.PaginationToken))
            return false;

        var client = GetGraphService(acc);

        var page = await client.Me.MailFolders["Inbox"].Messages
            .WithUrl(acc.PaginationToken)
            .GetAsync(cancellationToken: token);

        if (page?.Value is null || page.Value.Count == 0)
        {
            acc.NoMoreOlderEmail = true;
            await _storageService.UpdatePaginationAndNextTokenAsync(acc);
            return false;
        }

        foreach (var msg in page.Value)
        {
            var email = CreateEmailFromGraph(acc, msg);

            if (acc.Emails.Any(x => x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
                continue;

            acc.Emails.Add(email);
            await _storageService.StoreEmailAsync(acc, email);
        }

        acc.PaginationToken = page.OdataNextLink;
        acc.NoMoreOlderEmail = string.IsNullOrEmpty(acc.PaginationToken);

        await _storageService.UpdatePaginationAndNextTokenAsync(acc);

        return true;
    }

    public Provider GetProvider()
    {
        return Provider.Microsoft;
    }
}