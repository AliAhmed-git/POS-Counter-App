using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace PosApp.Desktop.Views
{
    public partial class PasswordDialog : Window
    {
        public bool IsVerified { get; private set; }
        public string PromptTitle { get; set; } = "SUPERVISOR OVERRIDE";
        private string[] _requiredPasswords;

        public PasswordDialog(string requiredPassword, string? title = null) : this(new[] { requiredPassword }, title)
        {
        }

        public PasswordDialog(string[] requiredPasswords, string? title = null)
        {
            InitializeComponent();
            _requiredPasswords = requiredPasswords.Where(p => !string.IsNullOrEmpty(p)).ToArray();
            if (!string.IsNullOrEmpty(title)) PromptTitle = title;
            DataContext = this;
            Loaded += (s, e) => PasswordInput.Focus();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Verify();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsVerified = false;
            Close();
        }

        private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
        {
            ErrorMessage.Visibility = Visibility.Hidden; // Hide error when typing
            if (e.Key == Key.Enter)
            {
                Verify();
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }

        private void Verify()
        {
            if (_requiredPasswords.Contains(PasswordInput.Password))
            {
                IsVerified = true;
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorMessage.Text = "INCORRECT PASSWORD";
                ErrorMessage.Visibility = Visibility.Visible;
                
                // Trigger Shake Animation
                if (FindResource("ErrorShake") is System.Windows.Media.Animation.Storyboard sb)
                {
                    sb.Begin();
                }

                PasswordInput.Clear();
                PasswordInput.Focus();
            }
        }
    }
}
