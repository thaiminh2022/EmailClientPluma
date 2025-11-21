using Microsoft.Web.WebView2.Wpf;
using System.Windows;

namespace EmailClientPluma.MVVM.Views
{
    /// <summary>
    /// Interaction logic for NewEmailWindow.xaml
    /// </summary>
    public partial class NewEmailView : Window
    {
        public NewEmailView()
        {
            InitializeComponent();
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            OptionsPopup.IsOpen = !OptionsPopup.IsOpen;
        }

        private void LabelsButton_Click(object sender, RoutedEventArgs e)
        {
            LabelsPopup.IsOpen = !LabelsPopup.IsOpen;
        }

        private async void WebViewControl_Loaded(object sender, RoutedEventArgs e)
        {
            //var wv2 = (WebView2)sender;

            //await wv2.EnsureCoreWebView2Async();

            //string editorPath = System.IO.Path.Combine(
            //    AppDomain.CurrentDomain.BaseDirectory,
            //    "QuillEditor",
            //    "index.html");

            //wv2.CoreWebView2.Navigate(new Uri(editorPath).AbsoluteUri);

        }
    }
}
