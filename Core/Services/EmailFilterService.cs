using EmailClientPluma.Core.Models;
using System.Windows.Interop;

public class EmailFilterOptions
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string HasWords { get; set; } = "";
    public string DoesNotHave { get; set; } = "";

    public DateTime? SelectedDate { get; set; } = null;
    public short DateRangeIndex { get; set; } = -1;

    public short SizeOperatorIndex { get; set; } = 0; // 0=greater than,1=less than
    public double SizeValue { get; set; } = 0; // numeric value
    public short SizeUnitIndex { get; set; } = 0; // 0=MB,1=KB

    public string SearchText { get; set; } = string.Empty;

    public int MailboxIndex { get; set; } = 0; // 0=All Mail,1=Inbox,2=Sent
    public bool HasAttachment { get; set; } = false;

}



interface IEmailFilterService
{
    Task<bool> MatchFiltersAsync(Email email, EmailFilterOptions options, CancellationToken cancellationToken = default);
}

class EmailFilterService : IEmailFilterService
{
    public async Task<bool> MatchFiltersAsync(Email emailObj, EmailFilterOptions opt, CancellationToken cancellationToken = default)
    {
        if (emailObj == null) return false;

        // simulate async for large collections
        await Task.Yield(); // ensures this is async and doesn't block UI

        cancellationToken.ThrowIfCancellationRequested();

        var email = emailObj.MessageParts;

        if (!string.IsNullOrWhiteSpace(opt.From) &&
            !email.From.Contains(opt.From, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(opt.To) &&
            !email.To.Contains(opt.To, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(opt.Subject) &&
            !email.Subject.Contains(opt.Subject, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(opt.HasWords) &&
            !(email.Body?.Contains(opt.HasWords, StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        if (!string.IsNullOrWhiteSpace(opt.DoesNotHave) &&
            (email.Body?.Contains(opt.DoesNotHave, StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        //Date
        if (email.Date.HasValue && opt.SelectedDate.HasValue)
        {
            DateTime endDate = opt.SelectedDate.Value;
            DateTime startDate = opt.SelectedDate.Value;

            switch (opt.DateRangeIndex)
            {
                case 0: // 1 day
                    startDate = endDate.AddDays(-1);
                    break;
                case 1: // 1 week
                    startDate = endDate.AddDays(-7);
                    break;
                case 2: // 1 month
                    startDate = endDate.AddMonths(-1);
                    break;
                default:
                    break;
            }

            if (email.Date.HasValue)
            {
                var emailDate = email.Date.Value.DateTime;
                if (emailDate < startDate || emailDate > endDate)
                    return false;
            }
        }

        //size
        if (opt.SizeValue > 0)
        {
            double emailSizeKb = email.EmailSizeInKb;
            double filterSizeKb = opt.SizeValue * (opt.SizeUnitIndex == 0 ? 1024 : 1); // MB->KB

            switch (opt.SizeOperatorIndex)
            {
                case 0: // greater than
                    if (emailSizeKb < filterSizeKb) return false;
                    break;
                case 1: // less than
                    if (emailSizeKb > filterSizeKb) return false;
                    break;
                default:
                    break;
            }
        }

        // --- Mailbox filter ---
        switch (opt.MailboxIndex)
        {
            case 1: // Inbox
               
                break;
            case 2: // Sent
                
                break;
        }



        if (!string.IsNullOrWhiteSpace(opt.SearchText))
        {
            bool match = (email.Subject?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (email.From?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (email.To?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (email.Body?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!match) return false;
        }

        if(opt.HasAttachment)
        {
            if (email.Attachments == null || !email.Attachments.Any())
                return false;
        }

        return true;
    }
}