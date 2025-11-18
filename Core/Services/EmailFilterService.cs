using EmailClientPluma.Core.Models;
using System.ComponentModel;
using System.Windows.Interop;

public class EmailFilterOptions : INotifyPropertyChanged
{
    private string _from = "";
    public string From
    {
        get => _from;
        set { if (_from != value) { _from = value; OnPropertyChanged(); } }
    }

    private string _to = "";
    public string To
    {
        get => _to;
        set { if (_to != value) { _to = value; OnPropertyChanged(); } }
    }

    private string _subject = "";
    public string Subject
    {
        get => _subject;
        set { if (_subject != value) { _subject = value; OnPropertyChanged(); } }
    }

    private string _hasWords = "";
    public string HasWords
    {
        get => _hasWords;
        set { if (_hasWords != value) { _hasWords = value; OnPropertyChanged(); } }
    }

    private string _doesNotHave = "";
    public string DoesNotHave
    {
        get => _doesNotHave;
        set { if (_doesNotHave != value) { _doesNotHave = value; OnPropertyChanged(); } }
    }

    private DateTime? _selectedDate = null;
    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set { if (_selectedDate != value) { _selectedDate = value; OnPropertyChanged(); } }
    }

    private short _dateRangeIndex = -1;
    public short DateRangeIndex
    {
        get => _dateRangeIndex;
        set { if (_dateRangeIndex != value) { _dateRangeIndex = value; OnPropertyChanged(); } }
    }

    private short _sizeOperatorIndex = 0;
    public short SizeOperatorIndex
    {
        get => _sizeOperatorIndex;
        set { if (_sizeOperatorIndex != value) { _sizeOperatorIndex = value; OnPropertyChanged(); } }
    }

    private double _sizeValue = 0;
    public double SizeValue
    {
        get => _sizeValue;
        set { if (_sizeValue != value) { _sizeValue = value; OnPropertyChanged(); } }
    }

    private short _sizeUnitIndex = 0;
    public short SizeUnitIndex
    {
        get => _sizeUnitIndex;
        set { if (_sizeUnitIndex != value) { _sizeUnitIndex = value; OnPropertyChanged(); } }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (_searchText != value) { _searchText = value; OnPropertyChanged(); } }
    }

    private int _mailboxIndex = 0;
    public int MailboxIndex
    {
        get => _mailboxIndex;
        set { if (_mailboxIndex != value) { _mailboxIndex = value; OnPropertyChanged(); } }
    }

    private bool _hasAttachment = false;
    public bool HasAttachment
    {
        get => _hasAttachment;
        set { if (_hasAttachment != value) { _hasAttachment = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
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

        // HasWords: must be in subject OR body
        if (!string.IsNullOrWhiteSpace(opt.HasWords))
        {
            bool contains = (email.Subject?.Contains(opt.HasWords, StringComparison.OrdinalIgnoreCase) ?? false)
                            || (email.Body?.Contains(opt.HasWords, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!contains)
                return false;
        }

        // DoesNotHave: must NOT be in subject AND body
        if (!string.IsNullOrWhiteSpace(opt.DoesNotHave))
        {
            bool contains = (email.Subject?.Contains(opt.DoesNotHave, StringComparison.OrdinalIgnoreCase) ?? false)
                            || (email.Body?.Contains(opt.DoesNotHave, StringComparison.OrdinalIgnoreCase) ?? false);

            if (contains)
                return false;
        }

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