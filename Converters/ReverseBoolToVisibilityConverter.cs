using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EmailClientPluma.Converters
{
    public class ReverseBoolToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return Visibility.Visible;
            }

            // If value is boolean
            if (value is bool booleanValue)
            {
                // If true -> Collapsed (Hidden)
                // If false -> Visible
                return booleanValue ? Visibility.Hidden : Visibility.Visible;
            }

            return Visibility.Hidden;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is Visibility.Collapsed;
        }
    }
}