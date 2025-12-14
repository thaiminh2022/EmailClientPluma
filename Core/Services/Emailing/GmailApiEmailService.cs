using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Storaging;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using MimeKit;
using MessagePart = Google.Apis.Gmail.v1.Data.MessagePart;


namespace EmailClientPluma.Core.Services.Emailing;

internal class GmailApiEmailService : IEmailService
{
    private readonly IStorageService _storageService;
    private const int INITIAL_HEADER_WINDOW = 20;

    public GmailApiEmailService(IStorageService storageService)
    {
        _storageService = storageService;
    }
    
    #region Helper

    private GmailService CreateGmailService(Account acc)
    {
        var credentials = GoogleCredential.FromAccessToken(acc.Credentials.SessionToken);

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credentials, 
            ApplicationName = "EmailClientPluma",
        });
    }
    
    private async Task<string?> GetLastSyncedHistoryIdAsync(Account acc)
    {
        var emails = await _storageService.GetEmailsAsync(acc);
        return emails
            .Where(e => !string.IsNullOrEmpty(e.MessageIdentifiers.ProviderHistoryId))
            .OrderByDescending(e => ulong.Parse(e.MessageIdentifiers.ProviderHistoryId?? "0"))
            .Select(e => e.MessageIdentifiers.ProviderHistoryId)
            .FirstOrDefault();
    }
    private async Task<string?> GetOldestSyncedMessageIdAsync(Account acc)
    {
        var emails = await _storageService.GetEmailsAsync(acc);
        return emails
            .OrderBy(e => e.MessageParts.Date)
            .Select(e => e.MessageIdentifiers.ProviderMessageId)
            .FirstOrDefault();
    }
    private async Task ProcessAndStoreMessage(Account acc, Message msg)
    {
        var email = CreateEmailFromMessage(acc, msg);

        if (acc.Emails.Any(x => x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
            return;

        acc.Emails.Add(email);
        await _storageService.StoreEmailAsync(acc, email);
    }
    private string DecodeBase64Url(string base64Url)
    {
        // Gmail uses base64url encoding (RFC 4648)
        string base64 = base64Url.Replace('-', '+').Replace('_', '/');
            
        // Add padding if needed
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        byte[] data = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(data);
    }
    private MessagePart? FindPartByMimeType(IList<MessagePart> parts, string mimeType)
    {
        foreach (var part in parts)
        {
            if (part.MimeType == mimeType)
                return part;

            if (part.Parts != null)
            {
                var found = FindPartByMimeType(part.Parts, mimeType);
                if (found != null)
                    return found;
            }
        }
        return null;
    }
    private string ExtractBodyFromMessage(Message message)
    {
        if (message.Payload == null)
            return "(No Body)";

        // Check if body is directly in payload
        if (!string.IsNullOrEmpty(message.Payload.Body?.Data))
        {
            return DecodeBase64Url(message.Payload.Body.Data);
        }

        // Check parts for HTML or text
        if (message.Payload.Parts != null)
        {
            // Prefer HTML
            var htmlPart = FindPartByMimeType(message.Payload.Parts, "text/html");
            if (htmlPart != null && !string.IsNullOrEmpty(htmlPart.Body?.Data))
            {
                return DecodeBase64Url(htmlPart.Body.Data);
            }

            // Fallback to plain text
            var textPart = FindPartByMimeType(message.Payload.Parts, "text/plain");
            if (textPart != null && !string.IsNullOrEmpty(textPart.Body?.Data))
            {
                return DecodeBase64Url(textPart.Body.Data);
            }
        }

        return "(No Body)";
    }
    private Email CreateEmailFromMessage(Account acc, Message message)
    {
        var indentifier = new Email.Identifiers
        {
            OwnerAccountId = acc.ProviderUID,
            ProviderMessageId = message.Id,
            ProviderThreadId = message.ThreadId,
            ProviderHistoryId = message.HistoryId?.ToString(),
            FolderFullName = "INBOX",
            Provider = EmailProvider.Gmail,
            Flags = EmailIdentifierExtensions.FromGmailLabels(message.LabelIds),
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
        {
            foreach (var header in message.Payload.Headers)
            {
                
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
                            data.Date = date;
                        break;
                    case "message-id":
                        indentifier.InternetMessageId = header.Value;
                        break;
                    case "in-reply-to":
                        indentifier.InReplyTo = header.Value;
                        break;
                }
            }
        }

        // Set size
        if (message.SizeEstimate.HasValue)
            data.EmailSizeInKb = message.SizeEstimate.Value / 1024.0;

        return new Email(indentifier, data);
    }
    #endregion
    
    public async Task FetchEmailHeaderAsync(Account acc)
    {
        var service = CreateGmailService(acc);
        var lastHistoryId = await GetLastSyncedHistoryIdAsync(acc);

        ListMessagesResponse response;
        
        if (lastHistoryId is null)
        {
            // New batch
            var request = service.Users.Messages.List("me");
            request.MaxResults = INITIAL_HEADER_WINDOW;
            request.LabelIds = "INBOX";
            
            response = await request.ExecuteAsync();
        }
        else
        {
            // Incremental fetch
            try
            {
                var historyRequest = service.Users.History.List("me");
                historyRequest.StartHistoryId = ulong.Parse(lastHistoryId);
                historyRequest.HistoryTypes = UsersResource.HistoryResource.ListRequest.HistoryTypesEnum.MessageAdded;
                var historyResponse = await historyRequest.ExecuteAsync();
                
                if (historyResponse.History is null || historyResponse.History.Count == 0)
                {
                    // No new messages
                    return;
                }
                
                // Extract message IDs from history
                var newMessageIds = historyResponse.History
                    .Where(h => h.MessagesAdded is not null)
                    .SelectMany(h => h.MessagesAdded)
                    .Select(m => m.Message.Id)
                    .Distinct()
                    .ToList();
                
                if (newMessageIds.Count == 0)
                    return;
                
                // Fetch these specific messages
                foreach (var messageId in newMessageIds)
                {
                    var msg = await service.Users.Messages.Get("me", messageId).ExecuteAsync();
                    await ProcessAndStoreMessage(acc, msg);
                }

                return;
            }
            catch (Exception)
            {
                // If history fails, fall back to standard list
                var request = service.Users.Messages.List("me");
                request.MaxResults = INITIAL_HEADER_WINDOW;
                request.LabelIds = "INBOX";
                    
                response = await request.ExecuteAsync();
            }
        }
        // Process messages from list response
        if (response.Messages != null)
        {
            foreach (var msgRef in response.Messages)
            {
                // Fetch full message details
                var msg = await service.Users.Messages.Get("me", msgRef.Id).ExecuteAsync();
                await ProcessAndStoreMessage(acc, msg);
            }
        }
    }

    public async Task FetchEmailBodyAsync(Account acc, Email email)
    {
        using var service = CreateGmailService(acc);

        try
        {
            var msg = await service.Users.Messages.Get("me", email.MessageIdentifiers.ProviderMessageId).ExecuteAsync();
            email.MessageParts.Body = ExtractBodyFromMessage(msg);
            await _storageService.UpdateEmailBodyAsync(email);
        }
        catch (Exception ex)
        {
            MessageBoxHelper.Error(ex.Message);
            email.MessageParts.Body = "(Unable to fetch body)";
        }
    }



    public  async Task PrefetchRecentBodiesAsync(Account acc, int maxToPrefetch = 30)
    {
        var candidates = acc.Emails
            .Where(e => !e.BodyFetched)
            .OrderByDescending(e => e.MessageParts.Date)
            .Take(maxToPrefetch)
            .ToList();

        if (candidates.Count == 0)
            return;

        using var service = CreateGmailService(acc);
            
        try
        {
            foreach (var candidate in candidates)
            {
                var msg = await service.Users.Messages.Get("me", candidate.MessageIdentifiers.ProviderMessageId).ExecuteAsync();
                candidate.MessageParts.Body = ExtractBodyFromMessage(msg);
                await _storageService.UpdateEmailBodyAsync(candidate);
            }
        }
        catch (Exception ex)
        {
            MessageBoxHelper.Error(ex.Message);
        }
    }

    public async Task<bool> FetchOlderHeadersAsync(Account acc, int window, CancellationToken token = default)
    {
        if (acc.NoMoreOlderEmail) 
            return false;

        using var service = CreateGmailService(acc);

        var oldestMessageId = await GetOldestSyncedMessageIdAsync(acc);

        if (oldestMessageId == null)
            return false;

        var request = service.Users.Messages.List("me");
        request.MaxResults = window;
        request.LabelIds = "INBOX";
        request.PageToken = oldestMessageId; // Use as page token to get older messages
            
        var response = await request.ExecuteAsync();

        if (response.Messages == null || response.Messages.Count == 0)
        {
            acc.NoMoreOlderEmail = true;
            return false;
        }

        // Fetch and store older messages
        foreach (var msgRef in response.Messages)
        {
            var msg = await service.Users.Messages.Get("me", msgRef.Id).ExecuteAsync();
            var email = CreateEmailFromMessage(acc, msg);

            if (acc.Emails.Any(x => x.MessageIdentifiers.ProviderMessageId == email.MessageIdentifiers.ProviderMessageId))
                continue;

            acc.Emails.Add(email);
            await _storageService.StoreEmailAsync(acc, email);
        }

        return true;
    }
    private string EncodeBase64Url(string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);
        string base64 = Convert.ToBase64String(data);
        return base64.Replace('+', '-').Replace('/', '_').Replace("=", "");
    }

    private string CreateRfc2822Email(Account acc, Email.OutgoingEmail email)
    {
        var message = new MimeMessage();
        var address = MailboxAddress.Parse(acc.Email);
        address.Name = acc.DisplayName;
        message.From.Add(address);

        message.Subject = email.Subject;

        if (!string.IsNullOrEmpty(email.InReplyTo))
            message.InReplyTo = email.InReplyTo;

        if (!string.IsNullOrEmpty(email.ReplyTo))
        {
            foreach (var item in email.ReplyTo.Split(','))
            {
                message.ReplyTo.Add(InternetAddress.Parse(item.Trim()));
            }
        }

        foreach (var item in email.To.Split(','))
        {
            message.To.Add(InternetAddress.Parse(item.Trim()));
        }

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = email.Body
        };

        message.Body = bodyBuilder.ToMessageBody();
        message.Date = email.Date ?? DateTimeOffset.Now;

        using var memoryStream = new MemoryStream();
        message.WriteTo(memoryStream);
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    public async Task SendEmailAsync(Account acc, Email.OutgoingEmail email)
    {
        using var service = CreateGmailService(acc);

        // Create RFC 2822 formatted email
        var rawEmail = CreateRfc2822Email(acc, email);
        var base64UrlEmail = EncodeBase64Url(rawEmail);

        var gmailMessage = new Message
        {
            Raw = base64UrlEmail
        };

        await service.Users.Messages.Send(gmailMessage, "me").ExecuteAsync();
    }
}