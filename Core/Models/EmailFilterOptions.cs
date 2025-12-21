namespace EmailClientPluma.Core.Models;

internal class EmailFilterOptions : ObserableObject
{
    private short _dateRangeIndex = -1;

    private string _doesNotHave = "";
    private string _from = "";

    private bool _hasAttachment;

    private string _hasWords = "";


    private string _searchText = "";

    private DateTime? _selectedDate;

    private short _sizeOperatorIndex;

    private short _sizeUnitIndex;

    private double _sizeValue;

    private string _subject = "";

    private string _to = "";

    private EmailLabel? _selectedLabel;


    public string From
    {
        get => _from;
        set
        {
            if (_from != value)
            {
                _from = value;
                OnPropertyChanges();
            }
        }
    }

    public string To
    {
        get => _to;
        set
        {
            if (_to != value)
            {
                _to = value;
                OnPropertyChanges();
            }
        }
    }

    public string Subject
    {
        get => _subject;
        set
        {
            if (_subject != value)
            {
                _subject = value;
                OnPropertyChanges();
            }
        }
    }

    public string HasWords
    {
        get => _hasWords;
        set
        {
            if (_hasWords != value)
            {
                _hasWords = value;
                OnPropertyChanges();
            }
        }
    }

    public string DoesNotHave
    {
        get => _doesNotHave;
        set
        {
            if (_doesNotHave != value)
            {
                _doesNotHave = value;
                OnPropertyChanges();
            }
        }
    }

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate != value)
            {
                _selectedDate = value;
                OnPropertyChanges();
            }
        }
    }

    public short DateRangeIndex
    {
        get => _dateRangeIndex;
        set
        {
            if (_dateRangeIndex != value)
            {
                _dateRangeIndex = value;
                OnPropertyChanges();
            }
        }
    }

    public short SizeOperatorIndex
    {
        get => _sizeOperatorIndex;
        set
        {
            if (_sizeOperatorIndex != value)
            {
                _sizeOperatorIndex = value;
                OnPropertyChanges();
            }
        }
    }

    public double SizeValue
    {
        get => _sizeValue;
        set
        {
            if (_sizeValue != value)
            {
                _sizeValue = value;
                OnPropertyChanges();
            }
        }
    }

    public short SizeUnitIndex
    {
        get => _sizeUnitIndex;
        set
        {
            if (_sizeUnitIndex != value)
            {
                _sizeUnitIndex = value;
                OnPropertyChanges();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanges();
            }
        }
    }


    public bool HasAttachment
    {
        get => _hasAttachment;
        set
        {
            if (_hasAttachment != value)
            {
                _hasAttachment = value;
                OnPropertyChanges();
            }
        }
    }

    public EmailLabel? SelectedLabel
    {
        get => _selectedLabel;
        set
        {
            _selectedLabel = value;
            if (_selectedLabel is not null)
            {
                OnPropertyChanges();
            }
        }
    }
}