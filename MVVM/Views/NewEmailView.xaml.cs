
using System;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

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

        // Toggle font popup visibility
        private void FontButton_Click(object sender, RoutedEventArgs e)
        {
            FontPopup.IsOpen = !FontPopup.IsOpen;
        }

        // --- Font family ---
        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyComboBox.SelectedItem is ComboBoxItem item)
                InputBox.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(item.Content.ToString()));
        }

        // --- Font size ---
        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSizeComboBox.SelectedItem is ComboBoxItem selected &&
                double.TryParse(selected.Content.ToString(), out double newSize))
                InputBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, newSize);
        }

        // --- Bold ---
        private void BoldButton_Checked(object sender, RoutedEventArgs e)
        {
            InputBox.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
        }

        private void BoldButton_Unchecked(object sender, RoutedEventArgs e)
        {
            InputBox.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
        }

        // --- Italic ---
        private void ItalicButton_Checked(object sender, RoutedEventArgs e)
        {
            InputBox.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
        }

        private void ItalicButton_Unchecked(object sender, RoutedEventArgs e)
        {
            InputBox.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
        }

        // --- Underline ---
        private void UnderlineButton_Checked(object sender, RoutedEventArgs e)
        {
            InputBox.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
        }

        private void UnderlineButton_Unchecked(object sender, RoutedEventArgs e)
        {
            InputBox.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
        }

        // --- Reflect current selection to toolbar state ---
        private void InputBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            object weight = InputBox.Selection.GetPropertyValue(TextElement.FontWeightProperty);
            object style = InputBox.Selection.GetPropertyValue(TextElement.FontStyleProperty);
            object deco = InputBox.Selection.GetPropertyValue(Inline.TextDecorationsProperty);

            BoldButton.IsChecked = (weight != DependencyProperty.UnsetValue) && weight.Equals(FontWeights.Bold);
            ItalicButton.IsChecked = (style != DependencyProperty.UnsetValue) && style.Equals(FontStyles.Italic);
            UnderlineButton.IsChecked = (deco != DependencyProperty.UnsetValue) && deco.Equals(TextDecorations.Underline);
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


    //Binding rich text box
    public static class RichTextBoxHelper
    {
        public static readonly DependencyProperty BoundDocumentProperty =
            DependencyProperty.RegisterAttached(
                "BoundDocument",
                typeof(string),
                typeof(RichTextBoxHelper),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundDocumentChanged));

        public static string GetBoundDocument(DependencyObject obj) => (string)obj.GetValue(BoundDocumentProperty);
        public static void SetBoundDocument(DependencyObject obj, string value) => obj.SetValue(BoundDocumentProperty, value);

        private static void OnBoundDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichTextBox richTextBox)
            {
                richTextBox.Document.Blocks.Clear();
                richTextBox.Document.Blocks.Add(new Paragraph(new Run(e.NewValue?.ToString() ?? "")));

                richTextBox.TextChanged -= RichTextBox_TextChanged;
                richTextBox.TextChanged += RichTextBox_TextChanged;
            }
        }

        private static void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var richTextBox = sender as RichTextBox;
            if (richTextBox == null) return;

            var textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
            SetBoundDocument(richTextBox, textRange.Text);
        }
    }
}
