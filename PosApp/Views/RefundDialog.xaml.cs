using System.Windows;
using PosApp.Desktop.ViewModels;

namespace PosApp.Desktop.Views
{
    public partial class RefundDialog : Window
    {
        public RefundDialog(RefundDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = (RefundDialogViewModel)DataContext;
            bool success = await viewModel.ProcessRefundAsync();
            
            if (success)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Failed to process refund. Please try again.", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
