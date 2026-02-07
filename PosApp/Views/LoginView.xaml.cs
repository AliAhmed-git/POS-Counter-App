using System.Windows;
using System.Windows.Controls;
using PosApp.Desktop.ViewModels;

namespace PosApp.Desktop.Views
{
    public partial class LoginView : UserControl
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private async void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (DataContext is LoginViewModel vm)
                {
                    if (sender is TextBox tb && tb.Name == "UsernameBox") // Identify Username box
                    {
                        await vm.ValidateUsernameCommand.ExecuteAsync(null);
                        
                        // Check if validation succeeded (StatusText cleared)
                        if (string.IsNullOrEmpty(vm.StatusText))
                        {
                             var element = e.OriginalSource as UIElement;
                             element?.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                        }
                    }
                    else if (sender == UserPasswordBox)
                    {
                        if (vm.LoginCommand.CanExecute(null))
                        {
                            await vm.LoginCommand.ExecuteAsync(null);
                        }
                    }
                    else 
                    {
                        // Default behavior for other fields (e.g. Counter)
                        var element = e.OriginalSource as UIElement;
                        element?.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                    }
                }
                e.Handled = true;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
            {
                vm.Password = pb.Password;
            }
        }
        private void ShowPassword_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            VisiblePasswordBox.Text = UserPasswordBox.Password;
            VisiblePasswordBox.Visibility = Visibility.Visible;
            UserPasswordBox.Visibility = Visibility.Collapsed;
        }

        private void ShowPassword_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            UserPasswordBox.Visibility = Visibility.Visible;
            VisiblePasswordBox.Visibility = Visibility.Collapsed;
            VisiblePasswordBox.Text = string.Empty;
            
            // Refocus password box and keep cursor at end
            UserPasswordBox.Focus();
        }
    }
}
