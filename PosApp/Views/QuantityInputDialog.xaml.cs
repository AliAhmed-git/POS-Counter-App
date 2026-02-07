using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PosApp.Desktop.Views
{
    public partial class QuantityInputDialog : Window
    {
        public decimal Quantity { get; private set; }
        public bool IsConfirmed { get; private set; }

        public QuantityInputDialog(decimal currentQuantity = 1m)
        {
            InitializeComponent();
            InputTextBox.Text = currentQuantity.ToString("0.###");
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Confirm();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirm();
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Allow digits
            if (char.IsDigit(e.Text, 0))
            {
                // If there's a decimal point, limit to 2 decimal places
                int decimalIndex = textBox.Text.IndexOf('.');
                if (decimalIndex != -1 && textBox.SelectionLength == 0)
                {
                    // If caret is after the decimal point
                    if (textBox.CaretIndex > decimalIndex)
                    {
                        string[] parts = textBox.Text.Split('.');
                        if (parts.Length > 1 && parts[1].Length >= 2)
                        {
                            e.Handled = true;
                            return;
                        }
                    }
                }
                
                e.Handled = false;
                return;
            }

            // Allow one decimal point
            if (e.Text == "." && !textBox.Text.Contains("."))
            {
                e.Handled = false;
                return;
            }

            e.Handled = true;
        }

        private void Confirm()
        {
            if (decimal.TryParse(InputTextBox.Text, out decimal result))
            {
                Quantity = result;
                IsConfirmed = true;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid numeric value.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                InputTextBox.SelectAll();
                InputTextBox.Focus();
            }
        }
    }
}
