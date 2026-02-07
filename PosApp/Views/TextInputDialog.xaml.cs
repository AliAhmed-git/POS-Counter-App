using System.Windows;
using System.Windows.Input;

namespace PosApp.Desktop.Views
{
    public partial class TextInputDialog : Window
    {
        public string Result { get; private set; } = string.Empty;

        public TextInputDialog(string prompt, string? defaultValue = "")
        {
            InitializeComponent();
            PromptTextBlock.Text = prompt.ToUpper();
            InputTextBox.Text = defaultValue ?? "";
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Result = InputTextBox.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OK_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }
    }
}
