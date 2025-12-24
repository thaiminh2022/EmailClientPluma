using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EmailClientPluma.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return Visibility.Visible;
            }

            if (value is bool booleanValue)
            {
                return booleanValue ? Visibility.Visible : Visibility.Hidden;
            }

            return Visibility.Hidden;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is Visibility.Visible;
        }
    }
}