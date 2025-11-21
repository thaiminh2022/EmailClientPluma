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

    }
}
