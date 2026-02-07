using System;
using System.Windows;
using PosApp.Desktop.ViewModels;
using PosApp.Desktop.Services;
using System.ComponentModel;

namespace PosApp.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly KioskHook _kioskHook;
        private string _barcodeBuffer = "";
        private DateTime _lastKeyPressTime = DateTime.MinValue;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            _kioskHook = new KioskHook();
            
            // Sync hook with initial state (ViewModel now defaults to true)
            if (viewModel.IsKioskMode)
            {
                _kioskHook.Start();
            }

            // Listen for changes to KioskMode
            viewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(MainViewModel.IsKioskMode))
                {
                    if (viewModel.IsKioskMode)
                        _kioskHook.Start();
                    else
                        _kioskHook.Stop();
                }
            };
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.IsKioskMode)
            {
                // Restrict F-keys that might exit or minimize
                if (e.Key == System.Windows.Input.Key.System && e.SystemKey == System.Windows.Input.Key.F4)
                {
                    e.Handled = true; // Disable Alt+F4
                    vm.StatusText = "Exit restricted in Kiosk Mode.";
                    return;
                }

                // EMERGENCY KILLSWITCH: Ctrl + Alt + Shift + End
                if (e.Key == System.Windows.Input.Key.End && 
                    (System.Windows.Input.Keyboard.Modifiers & (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt | System.Windows.Input.ModifierKeys.Shift)) == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt | System.Windows.Input.ModifierKeys.Shift))
                {
                    System.Windows.Application.Current.Shutdown();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == System.Windows.Input.Key.F9 || e.Key == System.Windows.Input.Key.F11)
            {
                if (DataContext is MainViewModel kioskVm)
                {
                    // Let the ViewModel handle the security logic in ToggleKioskModeCommand
                    kioskVm.ToggleKioskModeCommand.Execute(null);
                }
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.F7)
            {
                if (DataContext is MainViewModel mainVm && mainVm.CurrentViewModel is SaleViewModel saleVm)
                {
                    saleVm.SelectShiftItemsCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.F5)
            {
                if (DataContext is MainViewModel f5Vm && f5Vm.CurrentViewModel is SaleViewModel f5SaleVm)
                {
                    f5SaleVm.SelectPaymentMethodCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.F10)
            {
                if (DataContext is MainViewModel f10Vm)
                {
                    f10Vm.NavigateToDCRCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.F7)
            {
                if (DataContext is MainViewModel f7Vm)
                {
                    // If not on sale screen, go there first
                    if (!(f7Vm.CurrentViewModel is SaleViewModel))
                        f7Vm.NavigateToSaleCommand.Execute(null);
                    
                    if (f7Vm.CurrentViewModel is SaleViewModel f7SaleVm)
                        f7SaleVm.ReturnItemsCommand.Execute(null);
                    
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.S && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                if (DataContext is MainViewModel sVm)
                {
                    sVm.NavigateToSettingsCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.Q && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                if (DataContext is MainViewModel qVm)
                {
                    qVm.LogoutCommand.Execute(null);
                    e.Handled = true;
                }
            }
            
            // Global Barcode Capture Logic
            HandleGlobalBarcodeCapture(e);
        }

        private void HandleGlobalBarcodeCapture(System.Windows.Input.KeyEventArgs e)
        {
            // Only capture if we are on the Sale screen
            if (!(DataContext is MainViewModel mainVm) || !(mainVm.CurrentViewModel is SaleViewModel saleVm))
            {
                _barcodeBuffer = "";
                return;
            }

            // If focused on a TextBox or PasswordBox (other than the barcode box itself), don't interfere with typing
            var focusedElement = System.Windows.Input.Keyboard.FocusedElement;
            if (focusedElement is System.Windows.Controls.TextBox tb && tb.Name != "BarcodeBox" && tb.Name != "ProductSearchBox" || 
                focusedElement is System.Windows.Controls.PasswordBox)
            {
                _barcodeBuffer = "";
                return;
            }

            // Get the actual key (handles Alt keys correctly)
            System.Windows.Input.Key key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
            bool isDigit = (key >= System.Windows.Input.Key.D0 && key <= System.Windows.Input.Key.D9) ||
                           (key >= System.Windows.Input.Key.NumPad0 && key <= System.Windows.Input.Key.NumPad9);
            bool isEnter = (key == System.Windows.Input.Key.Enter);

            if (!isDigit && !isEnter) return;

            // Clear buffer if more than 100ms passed (too slow for a scanner)
            // BYPASS: If Alt key is held OR if the buffer has content and we just hit Enter, we allow it
            bool isAltPressed = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt;
            
            if (!isAltPressed && (DateTime.Now - _lastKeyPressTime).TotalMilliseconds > 100)
            {
                // Only clear if this isn't a submission of a manually typed (Alt) barcode
                if (!isEnter || string.IsNullOrEmpty(_barcodeBuffer))
                {
                    _barcodeBuffer = "";
                }
            }
            _lastKeyPressTime = DateTime.Now;

            // Handle Numeric Input
            if (isDigit)
            {
                string keyChar = (key >= System.Windows.Input.Key.NumPad0) 
                    ? (key - System.Windows.Input.Key.NumPad0).ToString() 
                    : (key - System.Windows.Input.Key.D0).ToString();
                
                _barcodeBuffer += keyChar;

                // If Alt is held, block the key from the UI and show feedback
                if (isAltPressed)
                {
                    mainVm.StatusText = $"[SIMULATOR] Capturing: {_barcodeBuffer}";
                    e.Handled = true;
                }
            }
            else if (key == System.Windows.Input.Key.Multiply || key == System.Windows.Input.Key.System && e.SystemKey == System.Windows.Input.Key.Multiply || (key == System.Windows.Input.Key.D8 && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift))
            {
                // Quantity-First Detection: e.g., "5*"
                if (!string.IsNullOrEmpty(_barcodeBuffer) && decimal.TryParse(_barcodeBuffer, out decimal qty))
                {
                    saleVm.QuantityMultiplier = qty;
                    mainVm.StatusText = $"QUANTITY SET TO: {qty}";
                    _barcodeBuffer = "";
                    e.Handled = true;
                }
            }
            else if (isEnter)
            {
                if (_barcodeBuffer.Length >= 4) // Typical minimum barcode length
                {
                    // Scan-to-Remove Detection: Hold Shift while Scanning/Enter
                    bool isShiftPressed = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
                    if (isShiftPressed)
                    {
                        saleVm.QuantityMultiplier = -Math.Abs(saleVm.QuantityMultiplier);
                        mainVm.StatusText = $"REMOVING: {_barcodeBuffer}";
                    }

                    mainVm.StatusText = isAltPressed ? $"[SIMULATOR] Submitting barcode: {_barcodeBuffer}" : mainVm.StatusText;
                    saleVm.Barcode = _barcodeBuffer;
                    _ = saleVm.ProcessBarcodeCommand.ExecuteAsync(null);
                    _barcodeBuffer = "";
                    e.Handled = true; // Prevent further processing of this Enter
                }
                else
                {
                    _barcodeBuffer = "";
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.IsKioskMode)
            {
                e.Cancel = true;
                vm.StatusText = "App closure restricted. Disable Kiosk Mode first.";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is MainViewModel mainVm)
            {
                // Trigger auto-backup on close (Fire and forget, but ideally we'd wait if it's quick)
                _ = mainVm.TriggerBackupAsync();
            }

            _kioskHook.Dispose();
            base.OnClosed(e);
        }

        private void SetFullScreen(bool enable)
        {
            if (DataContext is MainViewModel vm)
            {
                if (enable && !vm.IsKioskMode)
                    vm.ToggleKioskModeCommand.Execute(null);
            }
        }
    }
}
