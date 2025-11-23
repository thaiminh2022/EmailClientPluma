using System.Windows;
using System.Windows.Threading;

namespace EmailClientPluma.Core
{
    internal static class MessageBoxHelper
    {
        private static string DefaultTitle => "Pluma";

        public static void Info(params object[] message)
        {
            Show(message, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void Warning(params object[] message)
        {
            Show(message, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void Error(params object[] message)
        {
            Show(message, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static bool? Confirmation(params object[] message)
        {
            var res = Show(message, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return res switch {
                MessageBoxResult.Yes => true,
                MessageBoxResult.No => false,
                MessageBoxResult.None or _ => null
            };
        }

        public static MessageBoxResult Show(
            object[] message,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None
        )
        {
            MessageBoxResult result = MessageBoxResult.None;

            void Invoke()
            {
                result = MessageBox.Show(
                    string.Join("", message),
                    DefaultTitle,
                    buttons,
                    icon
                );
            }

            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(Invoke);
            else
                Invoke();

            return result;
        }
    }
}
