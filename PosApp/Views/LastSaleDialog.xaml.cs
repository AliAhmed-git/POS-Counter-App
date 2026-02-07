using System.Windows;
using System.Windows.Input;

namespace PosApp.Desktop.Views
{
    public partial class LastSaleDialog : Window
    {
        public enum LastSaleResult
        {
            Cancel,
            Print,
            Add
        }

        public LastSaleResult Result { get; private set; } = LastSaleResult.Cancel;
        public string InvoiceNo { get; private set; } = string.Empty;

        public LastSaleDialog(string lastInvoiceNo = "")
        {
            InitializeComponent();
            InvoiceNoTextBox.Text = lastInvoiceNo;
            InvoiceNoTextBox.Focus();
            InvoiceNoTextBox.SelectAll();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (Validate())
            {
                Result = LastSaleResult.Print;
                DialogResult = true;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (Validate())
            {
                Result = LastSaleResult.Add;
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = LastSaleResult.Cancel;
            DialogResult = false;
        }

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(InvoiceNoTextBox.Text))
            {
                MessageBox.Show("Please enter an invoice number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            InvoiceNo = InvoiceNoTextBox.Text;
            return true;
        }

        private void InvoiceNoTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Default to Print? Or just do nothing? 
                // User might prefer Enter to trigger the primary action (Add or Print)
                // Let's not bind Enter to a specific primary action to avoid accidents.
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
