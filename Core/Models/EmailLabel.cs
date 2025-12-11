using System.Windows.Media;

namespace EmailClientPluma.Core.Models
{
    internal class EmailLabel : ObserableObject
    {
        public int Id { get; set; } = -1; // for database use

        private string _name;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanges();
            }
        }

        // null is global for every account
        public string? OwnerAccountId { get; set; }

        private Color _color;

        public Color Color
        {
            get => _color;
            set { _color = value; OnPropertyChanges(); }
        }

        public bool IsEditable { get; set; }


        public EmailLabel(string name, Color color, bool isEditable)
        {
            _name = name;
            _color = color;
            IsEditable = isEditable;
        }


        // default values
        public static EmailLabel[] Labels => [Inbox, Sent, All];
        public static EmailLabel Inbox => new("Inbox", Color.FromArgb(255, 0, 120, 215), false);
        public static EmailLabel Sent => new("Sent", Color.FromArgb(255, 0, 178, 148), false);
        public static EmailLabel All => new("All", Color.FromArgb(255, 136, 108, 228), false);
    }
}
