using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Models.Exceptions;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Storaging;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Me.Messages.Item.Attachments.CreateUploadSession;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Newtonsoft.Json;
using System.IO;
using Account = EmailClientPluma.Core.Models.Account;
using Attachment = EmailClientPluma.Core.Models.Attachment;
using Message = Microsoft.Graph.Models.Message;

namespace EmailClientPluma.Core.Services.Emailing;

internal class OutlookApiEmailService(IMicrosoftClientApp clientApp, IStorageService storageService, ILogger<OutlookApiEmailService> logger)
    : IEmailService
{
    private const int INITIAL_HEADER_WINDOW = 20;

    private async Task<GraphServiceClient> GetGraphService(Account acc, bool forceCheckInternet = false)
    {
        logger.LogInformation("Getting graph service for {mail}", acc.Email);
        if (!await InternetHelper.HasInternetConnection(forceCheckInternet))
        {
            logger.LogError("No internet connection");
            throw new NoInternetException();
        }

        var provider = new MsalAccessTokenProvider(clientApp.PublicClient, clientApp.Scopes, acc.ProviderUID);
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
        logger.LogInformation("Staring fetching email for: {email}", acc.Email);
        var graphClient = await GetGraphService(acc);
        var (_, lastSync) = ParseAccountTokens(acc.PaginationToken, acc.LastSyncToken);

        try
        {
            if (lastSync is not null)
            {
                // incremental
                logger.LogInformation("Incremental fetching for {email}", acc.Email);
                await FetchIncremental(acc, graphClient, lastSync.Value);
            }
            else
            {
                // initial
                logger.LogInformation("User {email} is a first time fetcher", acc.Email);
                await FetchInitial(acc, graphClient);
            }


            acc.NoMoreOlderEmail = string.IsNullOrEmpty(acc.PaginationToken);
            await storageService.UpdatePaginationAndNextTokenAsync(acc);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot fetch email for {email}", acc.Email);
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
                await storageService.StoreEmailAsync(acc, email);
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
                await storageService.StoreEmailAsync(acc, email);
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
                await storageService.StoreEmailAsync(acc, newEmail);
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
                await storageService.StoreEmailAsync(acc, newEmail);
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
        // Flags 
        var flags = EmailFlags.None;

        if (fromSent)
        {
            flags |= EmailFlags.Sent;
        }

        if (msg.IsRead == true)
            flags |= EmailFlags.Seen;

        if (msg.IsDraft == true)
            flags |= EmailFlags.Draft;

        // NOTE: "Sent" is usually inferred by folder (Sent Items) rather than a flag on Message.
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

        e.Labels.Add(EmailLabel.All);
        e.Labels.Add(fromSent ? EmailLabel.Sent : EmailLabel.Inbox);

        return e;
    }

    public async Task FetchEmailBodyAsync(Account acc, Email email)
    {
        logger.LogInformation("Fetching body for {mail} with subject {subject}", acc.Email, email.MessageParts.Subject);
        var client = await GetGraphService(acc);

        // Get specific message with body
        try
        {
            var msg = await client.Me.Messages[email.MessageIdentifiers.ProviderMessageId]
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = ["body", "hasAttachments"];
                });

            email.MessageParts.Body = msg?.Body?.Content ?? "(No Body)";
            await storageService.UpdateEmailBodyAsync(email);


            if (msg?.HasAttachments is false) return;

            var atts = await client.Me.Messages[email.MessageIdentifiers.ProviderMessageId]
                .Attachments.GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "name", "size", "contentType"];
                });

            atts?.Value?.ForEach(x =>
            {
                if (x is not FileAttachment)
                    return;

                var att = new Attachment
                {
                    FileName = x.Name,
                    ContentType = x.ContentType ?? @"application/octet-stream",
                    FilePath = null,
                    SizeBytes = x.Size ?? 0,
                    ProviderAttachmentId = x.Id,
                    OwnerEmailId = email.MessageIdentifiers.EmailId
                };
                if (email.MessageParts.Attachments.Any(att.IsEqualOwnerName))
                {
                    return;
                }

                email.MessageParts.Attachments.Add(att);
             
            });
            await storageService.StoreAttachmentRefAsync(email);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Body fetching failed for {email} with subject {subject}", acc.Email, email.MessageParts.Subject);
            throw new EmailFetchException(inner: ex);
        }
    }

    public async Task FetchEmailAttachmentsAsync(Account acc, Email email)
    {
        logger.LogInformation("Fetching attachment for {mail} with subject {subject}", acc.Email, email.MessageParts.Subject);
        var client = await GetGraphService(acc);

        // Get specific message with body
        try
        {
            foreach (var attachment in email.MessageParts.Attachments)
            {
                if (attachment.ProviderAttachmentId is null) continue;

                var att = await client.Me
                    .Messages[email.MessageIdentifiers.ProviderMessageId]
                    .Attachments[attachment.ProviderAttachmentId]
                    .GetAsync(config =>
                    {
                        config.QueryParameters.Select = ["contentBytes"];
                    });

                if (att is not FileAttachment file) continue;
                if (file.ContentBytes is null) continue;

                var path = await storageService.UpdateAttachmentContentAsync(email, attachment, file.ContentBytes);
                attachment.FilePath = path;
            }

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Attachment fetching failed for {email} with subject {subject}", acc.Email, email.MessageParts.Subject);
            throw new AttachmentFetchingException(inner: ex);
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

        var client = await GetGraphService(acc);
        logger.LogInformation("Fetching recent body around for {email} with {n} zone", acc.Email, candidates.Count);

        foreach (var candidate in candidates)
        {
            try
            {
                var msg = await client.Me.Messages[candidate.MessageIdentifiers.ProviderMessageId]
                    .GetAsync(cfg =>
                    {
                        cfg.QueryParameters.Select = ["body", "hasAttachments"];
                    });

                candidate.MessageParts.Body = msg?.Body?.Content ?? "(No Body)";
                await storageService.UpdateEmailBodyAsync(candidate);


                if (msg?.HasAttachments is false) return;

                var atts = await client.Me.Messages[candidate.MessageIdentifiers.ProviderMessageId]
                    .Attachments.GetAsync(config =>
                    {
                        config.QueryParameters.Select = ["id", "name", "size", "contentType"];
                    });

                atts?.Value?.ForEach(x =>
                {
                    if (x is not FileAttachment)
                        return;
                    var att = new Attachment
                    {
                        FileName = x.Name,
                        ContentType = x.ContentType ?? @"application/octet-stream",
                        FilePath = null,
                        SizeBytes = x.Size ?? 0,
                        ProviderAttachmentId = x.Id,
                        OwnerEmailId = candidate.MessageIdentifiers.EmailId
                    };

                    if (candidate.MessageParts.Attachments.Any(att.IsEqualOwnerName))
                    {
                        return;
                    }
                    candidate.MessageParts.Attachments.Add(att);
                });
                await storageService.StoreAttachmentRefAsync(candidate);
            }
            catch (Exception ex)
            {
                // ignore, for logging only
                logger.LogError(ex, "Error fetching recent bodies for {email}", acc.Email);
                throw new EmailFetchException(inner: ex);
            }
        }
    }

    public async Task<bool> FetchOlderHeadersAsync(Account acc, int window, CancellationToken token = default)
    {
        if (acc.NoMoreOlderEmail)
            return false;
        logger.LogInformation("Fetching older headers for {email}", acc.Email);

        var (pag, _) = ParseAccountTokens(acc.PaginationToken, acc.LastSyncToken);
        if (pag is null)
            return false;

        var client = await GetGraphService(acc);

        var pagTok = new PaginationTok();

        if (pag.Value.InboxNextLink is not null)
        {
            // inbox
            MessageCollectionResponse? pageInbox;
            try
            {
                logger.LogInformation("Getting token for inbox");
                pageInbox = await client.Me.MailFolders["inbox"].Messages
                    .WithUrl(pag.Value.InboxNextLink)
                    .GetAsync(cancellationToken: token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting token for {tok}", acc.Email);
                throw new EmailTokenException(inner: ex);
            }

            if (pageInbox?.Value is null || pageInbox.Value.Count == 0)
            {
                logger.LogWarning("No more fetching since no more older email");
                acc.NoMoreOlderEmail = true;
                return false;
            }

            foreach (var msg in pageInbox.Value)
            {
                var email = CreateEmailFromGraph(acc, msg);

                if (acc.Emails.Any(x => x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
                    continue;

                acc.Emails.Add(email);
                await storageService.StoreEmailAsync(acc, email);
            }

            pagTok.InboxNextLink = pageInbox.OdataNextLink;
        }


        if (pag.Value.SentNextLink is not null)
        {

            // sent
            MessageCollectionResponse? pageSent;
            try
            {
                logger.LogInformation("Getting token for sent");
                pageSent = await client.Me.MailFolders["sentitems"].Messages
                    .WithUrl(pag.Value.SentNextLink)
                    .GetAsync(cancellationToken: token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting token for {tok}", acc.Email);
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
                    await storageService.StoreEmailAsync(acc, email);
                }
            }

            pagTok.SentNextLink = pageSent?.OdataNextLink;
        }


        acc.PaginationToken = JsonConvert.SerializeObject(pagTok);
        acc.NoMoreOlderEmail = string.IsNullOrEmpty(pagTok.InboxNextLink) && string.IsNullOrEmpty(pagTok.SentNextLink);

        await storageService.UpdatePaginationAndNextTokenAsync(acc);
        return true;
    }


    public async Task SendEmailAsync(Account acc, Email.OutgoingEmail email)
    {
        var client = await GetGraphService(acc, forceCheckInternet: true);
        try
        {
            // create a draft
            var draft = await CreateDraftAsync(client, acc, email);
            if (draft?.Id is null)
            {
                throw new EmailSendException();
            }

            // add attachments to draft
            foreach (var attachment in email.Attachments)
            {
                if (!File.Exists(attachment.FilePath))
                {
                    var ex = new AttachmentReadForSendingException();
                    logger.LogError(ex, "{name} does not exists", attachment.FileName);

                    throw ex;
                }

                await UploadAttachmentAsync(client, attachment, draft.Id, CancellationToken.None);
            }

            // send
            await client.Me.Messages[draft.Id].Send.PostAsync();
        }
        catch (Exception ex) when (ex is not EmailSendException or AttachmentReadForSendingException)
        {
            throw new EmailSendException(inner: ex);
        }

    }

    private async Task<Message?> CreateDraftAsync(GraphServiceClient client, Account acc, Email.OutgoingEmail email)
    {
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


        var draft = new Message
        {
            Subject = email.Subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Html,
                Content = email.Body ?? string.Empty
            },
            From = new Recipient()
            {
                EmailAddress = new EmailAddress()
                {
                    Address = email.From,
                    Name = acc.DisplayName
                }

            },
            SentDateTime = DateTime.Now,
            ToRecipients = email.To
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => new Recipient
                {
                    EmailAddress = new EmailAddress { Address = x.Trim() }
                })
                .ToList(),
            ReplyTo = replyToRecipients
        };

        return await client.Me.Messages.PostAsync(draft);
    }

    private async Task UploadAttachmentAsync(GraphServiceClient graph,
        Attachment attachment,
        string draftId,
        CancellationToken ct)
    {
        await using var stream = File.OpenRead(attachment.FilePath);

        var attachmentItem = new AttachmentItem
        {
            AttachmentType = AttachmentType.File,
            Name = attachment.FileName,
            Size = stream.Length
        };

        var uploadSession = await graph.Me
            .Messages[draftId]
            .Attachments
            .CreateUploadSession
            .PostAsync(
                new CreateUploadSessionPostRequestBody()
                {
                    AttachmentItem = attachmentItem
                },
                cancellationToken: ct);

        // 320 KB chunks are recommended by Microsoft
        const int chunkSize = 320 * 1024;

        var uploader = new LargeFileUploadTask<AttachmentItem>(
            uploadSession,
            stream,
            chunkSize);

        var result = await uploader.UploadAsync(cancellationToken: ct);

        if (!result.UploadSucceeded)
        {
            throw new InvalidOperationException($"Failed to upload attachment: {attachment.FileName}");
        }

    }

    public Provider GetProvider()
    {
        return Provider.Microsoft;
    }
}