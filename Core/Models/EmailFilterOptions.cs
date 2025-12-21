using System.ComponentModel;

namespace EmailClientPluma.Core.Models
{
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

