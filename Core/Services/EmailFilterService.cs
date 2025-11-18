using EmailClientPluma.Core.Models;

public class EmailFilterOptions
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string HasWords { get; set; } = "";
    public string DoesNotHave { get; set; } = "";
    public DateTime? SelectedDate { get; set; } = null;
    public int DateRangeIndex { get; set; } = 0;
    public string SearchText { get; set; } = "";
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

        if (opt.SelectedDate.HasValue && opt.SelectedDate.HasValue)
        {
            DateTime endDate = opt.SelectedDate.Value;
            DateTime startDate = opt.SelectedDate.Value;

            switch (opt.DateRangeIndex)
            {
                case 1: // 1 day
                    startDate = endDate.AddDays(-1);
                    break;
                case 2: // 1 week
                    startDate = endDate.AddDays(-7);
                    break;
                case 3: // 1 month
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

        if (!string.IsNullOrWhiteSpace(opt.SearchText))
        {
            bool match = (email.Subject?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (email.From?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (email.To?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (email.Body?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!match) return false;
        }

        return true;
    }
}