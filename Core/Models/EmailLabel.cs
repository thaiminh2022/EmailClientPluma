using System.Drawing;

namespace EmailClientPluma.Core.Models
{
    internal record EmailLabel
    {
        public string Name { get; private set; }

        // null is global for every account
        public string? OwnerAccountID { get; set; }

        public Color Color { get; private set; }

        public bool IsDeletable { get; private set; }
        public bool IsSystem { get; private set; }

        public bool IsEditable => !IsSystem;

        public EmailLabel(string name, Color color, bool isDeletable, bool isSystem)
        {
            Name = name;
            Color = color;
            IsDeletable = isDeletable;
            IsSystem = isSystem;
        }


        // default values
        public static EmailLabel[] Labels => [Inbox, Sent, All];
        public static EmailLabel Inbox => new("Inbox", Color.FromArgb(255, 0, 120, 215), false, true);
        public static EmailLabel Sent => new("Sent", Color.FromArgb(255, 0, 178, 148), false, true);
        public static EmailLabel All => new("All", Color.FromArgb(255, 136, 108, 228), false, true);
    }
}
