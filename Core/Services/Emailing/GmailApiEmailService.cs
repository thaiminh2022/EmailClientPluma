using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Models.Exceptions;
using EmailClientPluma.Core.Services.Storaging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.IO;
using System.Text;
using MessagePart = Google.Apis.Gmail.v1.Data.MessagePart;


namespace EmailClientPluma.Core.Services.Emailing;

internal class GmailApiEmailService(IStorageService storageService, ILogger<GmailApiEmailService> logger) : IEmailService
{
    private const int INITIAL_HEADER_WINDOW = 20;

    public async Task FetchEmailHeaderAsync(Account acc)
    {
        logger.LogInformation("Staring fetching email for: {email}", acc.Email);
        var service = await CreateGmailService(acc);
        var lastHistoryId = await EmailAPIHelper.GetLastSyncedHistoryIdAsync(acc, storageService);

        ListMessagesResponse? response = null;

        if (lastHistoryId is null)
        {
            logger.LogInformation("User {email} is a first time fetcher", acc.Email);
            try
            {
                // New batch
                var request = service.Users.Messages.List("me");
                request.MaxResults = INITIAL_HEADER_WINDOW;
                request.LabelIds = "INBOX";

                response = await request.ExecuteAsync();

                acc.PaginationToken = response.NextPageToken;
                if (string.IsNullOrEmpty(response.NextPageToken)) acc.NoMoreOlderEmail = true;
                await storageService.UpdatePaginationAndNextTokenAsync(acc);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cannot fetch email for {email}", acc.Email);
                throw new EmailFetchException(inner: ex);
            }

        }
        else
        {
            logger.LogInformation("Incremental fetching for {email}", acc.Email);

            // Incremental fetch
            try
            {
                var historyRequest = service.Users.History.List("me");
                historyRequest.StartHistoryId = ulong.Parse(lastHistoryId);
                historyRequest.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;
                var historyResponse = await historyRequest.ExecuteAsync();

                if (historyResponse.History is null || historyResponse.History.Count == 0)
                    // No new messages
                    return;

                // Extract message IDs from history
                var newMessageIds = historyResponse.History
                    .Where(h => h.MessagesAdded is not null)
                    .SelectMany(h => h.MessagesAdded)
                    .Select(m => m.Message.Id)
                    .Distinct()
                    .ToList();

                if (newMessageIds.Count == 0)
                    return;

                List<Email> newMessages = [];
                // Fetch these specific messages
                foreach (var messageId in newMessageIds)
                {
                    var msg = await service.Users.Messages.Get("me", messageId).ExecuteAsync();
                    var email = ProcessMessage(acc, msg);

                    if (email is not null)
                        newMessages.Add(email);
                }

                foreach (var msg in newMessages)
                {
                    await storageService.StoreEmailAsync(acc, msg);
                    acc.Emails.Add(msg);
                }

                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Incremental failed, trying to fetch normally");
                // If history fails, fall back to standard list
                var request = service.Users.Messages.List("me");
                request.MaxResults = INITIAL_HEADER_WINDOW;
                request.LabelIds = "INBOX";

                response = await request.ExecuteAsync();
            }
        }


        // Process messages from list response
        if (response?.Messages != null)
        {
            List<Email> newMessages = [];

            foreach (var msgRef in response.Messages)
            {
                // Fetch full message details
                var msg = await service.Users.Messages.Get("me", msgRef.Id).ExecuteAsync();
                var email = ProcessMessage(acc, msg);

                if (email is not null)
                    newMessages.Add(email);
            }

            newMessages.ForEach(x =>
            {
                acc.Emails.Add(x);
            });
            await storageService.StoreEmailAsync(acc);

        }
    }

    public async Task FetchEmailBodyAsync(Account acc, Email email)
    {
        logger.LogInformation("Fetching body for {mail} with subject {subject}", acc.Email, email.MessageParts.Subject);
        using var service = await CreateGmailService(acc);

        try
        {
            var msg = await service.Users.Messages.Get("me", email.MessageIdentifiers.ProviderMessageId).ExecuteAsync();
            email.MessageParts.Body = ExtractBodyFromMessage(msg);
            var attachmentRefs = ExtractAttachmentRefs(msg);

            // add attachment data, but not download, only download when clicked
            foreach (var attRef in attachmentRefs)
            {
                email.MessageParts.Attachments.Add(new Attachment
                {
                    FileName = attRef.FileName,
                    FilePath = null,
                    ContentType = attRef.MimeType,
                    ProviderAttachmentId = attRef.AttachmentId,
                    SizeBytes = attRef.Size ?? 0,
                    OwnerEmailId = email.MessageIdentifiers.EmailId
                });
            }

            await storageService.UpdateEmailBodyAsync(email);
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
        using var service = await CreateGmailService(acc);

        try
        {
            foreach (var att in email.MessageParts.Attachments)
            {
                var attachment = await service.Users.Messages.Attachments
                    .Get("me", email.MessageIdentifiers.ProviderMessageId, att.ProviderAttachmentId)
                    .ExecuteAsync();


                if (attachment is null) continue;
                var data = DecodeBase64UrlToBytes(attachment.Data);

                var path = await storageService.UpdateAttachmentContentAsync(email, att, data);
                att.FilePath = path;
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


        logger.LogInformation("Fetching recent body around for {email} with {n} zone", acc.Email, candidates.Count);
        using var service = await CreateGmailService(acc);

        try
        {
            foreach (var candidate in candidates)
            {
                var msg = await service.Users.Messages.Get("me", candidate.MessageIdentifiers.ProviderMessageId)
                    .ExecuteAsync();
                candidate.MessageParts.Body = ExtractBodyFromMessage(msg);

                var attachmentRefs = ExtractAttachmentRefs(msg);

                // add attachment data, but not download, only download when clicked
                foreach (var attRef in attachmentRefs)
                {
                    candidate.MessageParts.Attachments.Add(new Attachment
                    {
                        FileName = attRef.FileName,
                        FilePath = null,
                        ContentType = attRef.MimeType,
                        ProviderAttachmentId = attRef.AttachmentId,
                        SizeBytes = attRef.Size ?? 0,
                        OwnerEmailId = candidate.MessageIdentifiers.EmailId

                    });
                }

                await storageService.UpdateEmailBodyAsync(candidate);
                await storageService.StoreAttachmentRefAsync(candidate);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching recent bodies for {email}", acc.Email);
            throw new EmailFetchException(inner: ex);
        }
    }

    public async Task<bool> FetchOlderHeadersAsync(Account acc, int window, CancellationToken token = default)
    {
        if (acc.NoMoreOlderEmail)
            return false;

        logger.LogInformation("Fetching older headers for {email}", acc.Email);

        using var service = await CreateGmailService(acc);

        var request = service.Users.Messages.List("me");
        request.MaxResults = window;
        request.LabelIds = "INBOX";

        // Use the pagination token if we have one
        if (!string.IsNullOrEmpty(acc.PaginationToken)) request.PageToken = acc.PaginationToken;

        ListMessagesResponse? response;
        try
        {
            response = await request.ExecuteAsync(token);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting token for {tok}", acc.Email);
            throw new EmailTokenException(inner: ex);
        }

        if (response.Messages is null || response.Messages.Count == 0)
        {
            logger.LogWarning("No more fetching since no more older email");
            acc.NoMoreOlderEmail = true;
            return false;
        }

        acc.PaginationToken = response.NextPageToken;
        if (string.IsNullOrEmpty(response.NextPageToken)) acc.NoMoreOlderEmail = true;

        await storageService.UpdatePaginationAndNextTokenAsync(acc);

        // Fetch and store older messages


        foreach (var msgRef in response.Messages)
        {
            try
            {
                var msg = await service.Users.Messages.Get("me", msgRef.Id).ExecuteAsync(token);
                var email = CreateEmailFromMessage(acc, msg);

                if (acc.Emails.Any(x =>
                        x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
                    continue;

                acc.Emails.Add(email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cannot fetch message from refs");
                throw new EmailFetchException(inner: ex);
            }

        }
        await storageService.StoreEmailAsync(acc);
        return true;
    }

    public Provider GetProvider()
    {
        return Provider.Google;
    }

    public async Task SendEmailAsync(Account acc, Email.OutgoingEmail email)
    {
        using var service = await CreateGmailService(acc, forceCheckInternet: true);

        logger.LogInformation("Sending email for {email} with subject: {sub}", acc.Email, email.Subject);

        // Create RFC 2822 formatted email
        var rawEmail = CreateRfc2822Email(acc, email);
        var base64UrlEmail = EncodeBase64Url(rawEmail);

        var gmailMessage = new Message
        {
            Raw = base64UrlEmail,
        };
        try
        {
            await service.Users.Messages.Send(gmailMessage, "me").ExecuteAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sending email error");
            throw new EmailSendException(inner: ex);
        }
    }

    private string EncodeBase64Url(string text)
    {
        var data = Encoding.UTF8.GetBytes(text);
        var base64 = Convert.ToBase64String(data);
        return base64.Replace('+', '-').Replace('/', '_').Replace("=", "");
    }

    private string CreateRfc2822Email(Account acc, Email.OutgoingEmail email)
    {
        var message = new MimeMessage();
        var address = MailboxAddress.Parse(acc.Email);
        address.Name = acc.DisplayName; // name
        message.From.Add(address); // from
        message.Subject = email.Subject; // subject

        if (!string.IsNullOrEmpty(email.InReplyTo)) // in reply to
            message.InReplyTo = email.InReplyTo;

        if (!string.IsNullOrEmpty(email.ReplyTo)) // reply to
            foreach (var item in email.ReplyTo.Split(','))
                message.ReplyTo.Add(InternetAddress.Parse(item.Trim()));

        // to
        foreach (var item in email.To.Split(',')) message.To.Add(InternetAddress.Parse(item.Trim()));

        // building body
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = email.Body
        };

        // attachments
        foreach (var file in email.Attachments)
        {
            if (!File.Exists(file.FilePath))
            {
                var ex = new AttachmentReadForSendingException();
                logger.LogError(ex, "{file} no longer not exists, ignoring", file.FileName);
                throw ex;
            }
            bodyBuilder.Attachments.Add(file.FilePath);
        }
        
        message.Body = bodyBuilder.ToMessageBody(); // body
        message.Date = email.Date ?? DateTimeOffset.Now; // date




        // encoding
        using var memoryStream = new MemoryStream();
        message.WriteTo(memoryStream);
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    #region Helper

    private async Task<GmailService> CreateGmailService(Account acc, bool forceCheckInternet = false)
    {
        logger.LogInformation("Getting gmail service for {}", acc.Email);
        if (!await InternetHelper.HasInternetConnection(forceCheckInternet))
        {
            logger.LogError("No internet connection");
            throw new NoInternetException();
        }

        var credentials = GoogleCredential.FromAccessToken(acc.Credentials.SessionToken);
        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credentials,
            ApplicationName = "GmailClient",
        });
    }

    private Email? ProcessMessage(Account acc, Message msg)
    {
        var email = CreateEmailFromMessage(acc, msg);
        return acc.Emails.Any(x => x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId) ? null : email;
    }

    private string DecodeBase64UrlToUtf8(string base64Url)
    {
        var bytes = DecodeBase64UrlToBytes(base64Url);
        return Encoding.UTF8.GetString(bytes);
    }

    private byte[] DecodeBase64UrlToBytes(string base64Url)
    {
        // Gmail uses base64url encoding (RFC 4648)
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');

        // Add padding if needed
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var data = Convert.FromBase64String(base64);
        return data;
    }


    private MessagePart? FindPartByMimeType(IList<MessagePart> parts, string mimeType)
    {
        foreach (var part in parts)
        {
            if (part.MimeType == mimeType)
                return part;

            if (part.Parts == null) continue;


            var found = FindPartByMimeType(part.Parts, mimeType);
            if (found != null)
                return found;
        }

        return null;
    }


    public sealed record GmailAttachmentRef(
        string FileName,
        string MimeType,
        string? AttachmentId,
        long? Size
        );


    private List<GmailAttachmentRef> ExtractAttachmentRefs(Message message)
    {
        List<GmailAttachmentRef> result = [];
        if (message.Payload is null) return result;


        GetAttachment(message.Payload);
        return result;

        void GetAttachment(MessagePart part)
        {
            if (!string.IsNullOrWhiteSpace(part.Filename))
            {
                result.Add(new GmailAttachmentRef
                (
                    part.Filename,
                    part.MimeType ?? "application/octet-stream",
                    part.Body?.AttachmentId,
                    part.Body?.Size
                ));
            }

            if (part.Parts is null)
            {
                return;
            }
            foreach (var child in part.Parts)
            {
                GetAttachment(child);
            }
        }
    }
    private string ExtractBodyFromMessage(Message message)
    {
        if (message.Payload == null)
            return "(No Body)";

        // Check if body is directly in payload
        if (!string.IsNullOrEmpty(message.Payload.Body?.Data)) 
            return DecodeBase64UrlToUtf8(message.Payload.Body.Data);

        // Check parts for HTML or text
        if (message.Payload.Parts != null)
        {
            // Prefer HTML
            var htmlPart = FindPartByMimeType(message.Payload.Parts, "text/html");
            if (htmlPart != null && !string.IsNullOrEmpty(htmlPart.Body?.Data))
                return DecodeBase64UrlToUtf8(htmlPart.Body.Data);

            // Fallback to plain text
            var textPart = FindPartByMimeType(message.Payload.Parts, "text/plain");
            if (textPart != null && !string.IsNullOrEmpty(textPart.Body?.Data))
                return DecodeBase64UrlToUtf8(textPart.Body.Data);
        }

        return "(No Body)";
    }

    private Email CreateEmailFromMessage(Account acc, Message message)
    {
        var flags = EmailIdentifierExtensions.FromGmailLabels(message.LabelIds);

        var identifiers = new Email.Identifiers
        {
            OwnerAccountId = acc.ProviderUID,
            ProviderMessageId = message.Id,
            ProviderThreadId = message.ThreadId,
            ProviderHistoryId = message.HistoryId?.ToString(),
            FolderFullName = "INBOX",
            Provider = Provider.Google,
            Flags = flags
        };


        var data = new Email.DataParts
        {
            Subject = "",
            Body = null,
            From = "",
            To = "",
            Date = DateTime.Now,
            EmailSizeInKb = 0
        };

        // Parse headers
        if (message.Payload?.Headers != null)
            foreach (var header in message.Payload.Headers)
                switch (header.Name.ToLower())
                {
                    case "from":
                        data.From = header.Value;
                        break;
                    case "to":
                        data.To = header.Value;
                        break;
                    case "subject":
                        data.Subject = header.Value;
                        break;
                    case "date":
                        if (DateTimeOffset.TryParse(header.Value, out var date))
                        {
                            data.Date = date;
                        }
                        else if (message.InternalDate.HasValue)
                        {
                            var dto = DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate.Value);
                            data.Date = dto;
                        }

                        break;
                    case "message-id":
                        identifiers.InternetMessageId = header.Value;
                        break;
                    case "in-reply-to":
                        identifiers.InReplyTo = header.Value;
                        break;
                }

        // Set size
        if (message.SizeEstimate.HasValue)
            data.EmailSizeInKb = message.SizeEstimate.Value / 1024.0;

        var e = new Email(identifiers, data);

        e.Labels.Add(EmailLabel.All);
        e.Labels.Add(flags.HasFlag(EmailFlags.Sent) ? EmailLabel.Sent : EmailLabel.Inbox);

        return e;
    }

    #endregion
}