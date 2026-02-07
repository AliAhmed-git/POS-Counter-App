using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PosApp.Desktop.Views
{
    public partial class PaymentMethodDialog : Window
    {
        public PaymentCharge? SelectedMethod { get; private set; }
        public decimal TotalAmount { get; private set; }

        public PaymentMethodDialog(decimal totalAmount = 0)
        {
            InitializeComponent();
            TotalAmount = totalAmount;
            DataContext = this; // Set DataContext for binding
            LoadMethods();
        }

        private void LoadMethods()
        {
            try
            {
                string json = File.ReadAllText("payment_charges.json");
                var methods = JsonSerializer.Deserialize<List<PaymentCharge>>(json);
                if (methods != null)
                {
                    // Load logos for each payment method
                    foreach (var method in methods)
                    {
                        method.LogoPath = GetLogoPath(method.Method ?? string.Empty);
                    }
                    MethodsList.ItemsSource = methods;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading payment methods: {ex.Message}");
                Close();
            }
        }

        private string GetLogoPath(string methodName)
        {
            // Map payment method names to logo file names
            var logoMapping = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                { "Cash", null }, // No logo for cash
                { "Alfalah Card", "alfalah.png" },
                { "JazzCash", "jazzcash.png" },
                { "EasyPaisa", "easypaisa.png" },
                { "SadaPay", "sadapay.png" },
                { "Nayapay", "nayapay.png" },
                { "Other Bank Card", "other.png" },
                { "HBL Card", "hbl.png" },
                { "MCB Card", "mcb.png" },
                { "UBL Card", "ubl.png" },
                { "Meezan Card", "meezan.png" },
                { "Allied Bank Card", "allied_bank.png" },
                { "Bank Al Habib Card", "bank-al-habib-logo.png" },
                { "Faysal Bank Card", "faysal_bank_logo.png" },
                { "Raast", "Raast_ID.png" }
            };

            if (logoMapping.TryGetValue(methodName, out string? logoFile) && logoFile != null)
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "payment_logos", logoFile);
                if (File.Exists(logoPath))
                {
                    return logoPath;
                }
            }

            return string.Empty; // No logo found
        }

        private void Method_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PaymentCharge charge)
            {
                SelectedMethod = charge;
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class PaymentCharge
    {
        public string? Method { get; set; }
        public decimal ChargePercentage { get; set; }
        public decimal TaxPercentage { get; set; }
        public string? LogoPath { get; set; } // Path to logo image
    }
}
