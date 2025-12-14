using System.ComponentModel;

namespace EmailClientPluma.Core.Models;

public class EmailFilterOptions : INotifyPropertyChanged
{
    private short _dateRangeIndex = -1;

    private string _doesNotHave = "";
    private string _from = "";

    private bool _hasAttachment;

    private string _hasWords = "";

    private int _mailboxIndex;

    private string _searchText = "";

    private DateTime? _selectedDate;

    private short _sizeOperatorIndex;

    private short _sizeUnitIndex;

    private double _sizeValue;

    private string _subject = "";

    private string _to = "";

    public string From
    {
        get => _from;
        set
        {
            if (_from != value)
            {
                _from = value;
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
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
                OnPropertyChanged();
            }
        }
    }

    public int MailboxIndex
    {
        get => _mailboxIndex;
        set
        {
            if (_mailboxIndex != value)
            {
                _mailboxIndex = value;
                OnPropertyChanged();
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
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}