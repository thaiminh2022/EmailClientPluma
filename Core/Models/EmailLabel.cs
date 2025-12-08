using System.Windows.Media;

namespace EmailClientPluma.Core.Models
{
    internal record EmailLabel
    {
        public int Id { get; set; } = 0; // for database use
        public string Name { get; private set; }

        // null is global for every account
        public string? OwnerAccountId { get; private set; }

        public Color Color { get; private set; }

        public bool IsEditable { get; private set; }


        public EmailLabel(string name, Color color, bool isEditable)
        {
            Name = name;
            Color = color;
            IsEditable = isEditable;    
        }


        // default values
        public static EmailLabel[] Labels => [Inbox, Sent, All];
        public static EmailLabel Inbox => new("Inbox", Color.FromArgb(255, 0, 120, 215), false);
        public static EmailLabel Sent => new("Sent", Color.FromArgb(255, 0, 178, 148), false);
        public static EmailLabel All => new("All", Color.FromArgb(255, 136, 108, 228), false);
    }
}
