using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EmailClientPluma.Core
{
    public class ObserableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanges([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new(name));
        }
    }
}
