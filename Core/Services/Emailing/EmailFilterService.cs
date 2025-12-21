using EmailClientPluma.Core.Models;

namespace EmailClientPluma.Core.Services.Emailing;

internal interface IEmailFilterService
{
    Task<bool> MatchFiltersAsync(Email email, EmailFilterOptions options,
        CancellationToken cancellationToken = default);
}

internal class EmailFilterService : IEmailFilterService
{
    public async Task<bool> MatchFiltersAsync(Email? emailObj, EmailFilterOptions opt,
        CancellationToken cancellationToken = default)
    {
        if (emailObj is null) return false;

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

        // HasWords: must be in subject OR body
        if (!string.IsNullOrWhiteSpace(opt.HasWords))
        {
            var contains = (email.Subject?.Contains(opt.HasWords, StringComparison.OrdinalIgnoreCase) ?? false)
                           || (email.Body?.Contains(opt.HasWords, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!contains)
                return false;
        }

        // DoesNotHave: must NOT be in subject AND body
        if (!string.IsNullOrWhiteSpace(opt.DoesNotHave))
        {
            var contains = (email.Subject?.Contains(opt.DoesNotHave, StringComparison.OrdinalIgnoreCase) ?? false)
                           || (email.Body?.Contains(opt.DoesNotHave, StringComparison.OrdinalIgnoreCase) ?? false);

            if (contains)
                return false;
        }

        //Date
        if (email.Date.HasValue && opt.SelectedDate.HasValue)
        {
            var endDate = opt.SelectedDate.Value;
            var startDate = opt.SelectedDate.Value;

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
            var emailSizeKb = email.EmailSizeInKb;
            var filterSizeKb = opt.SizeValue * (opt.SizeUnitIndex == 0 ? 1024 : 1); // MB->KB

            switch (opt.SizeOperatorIndex)
            {
                case 0: // greater than
                    if (emailSizeKb < filterSizeKb) return false;
                    break;
                case 1: // less than
                    if (emailSizeKb > filterSizeKb) return false;
                    break;
            }
        }
        
        if (!emailObj.Labels.Any(x => x.Name.Equals(opt.SelectedLabel?.Name))) { return false; }

        if (!string.IsNullOrWhiteSpace(opt.SearchText))
        {
            var match = (email.Subject?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (email.From?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (email.To?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (email.Body?.Contains(opt.SearchText, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!match) return false;
        }

        if (opt.HasAttachment) return false;

        return true;
    }
}