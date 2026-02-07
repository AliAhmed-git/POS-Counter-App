using System;
using System.Text;
using System.Windows.Input;
using System.Windows.Controls;

namespace PosApp.Desktop.Views
{
    public partial class SaleView : UserControl
    {
        private DateTime _lastKeystrokeTime = DateTime.MinValue;
        private System.Text.StringBuilder _barcodeBuffer = new System.Text.StringBuilder();

        public SaleView()
        {
            InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;
        }

        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            // Disable automation peers for the entire view to prevent 
            // the "Value cannot be null. (Parameter 'item')" crash in DataGridItemAutomationPeer.
            return new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(this);
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PosApp.Desktop.ViewModels.SaleViewModel oldVm)
            {
                oldVm.RequestFocus -= OnRequestFocus;
                oldVm.RequestSelectAll -= OnRequestSelectAll;
            }
            if (e.NewValue is PosApp.Desktop.ViewModels.SaleViewModel newVm)
            {
                newVm.RequestFocus += OnRequestFocus;
                newVm.RequestSelectAll += OnRequestSelectAll;
            }
        }

        private void OnRequestSelectAll()
        {
            Dispatcher.Invoke(() => 
            {
                SaleGrid.Focus();
                SaleGrid.SelectAll();
            });
        }

        private void OnRequestFocus()
        {
             // Use Dispatcher to ensure UI thread validity if called from async task
             Dispatcher.Invoke(() => 
             {
                 BarcodeBox.Focus();
                 BarcodeBox.SelectAll();
             });
        }

        private void OnSidebarItemClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement element && element.DataContext is PosApp.Desktop.Models.Item item)
            {
                if (DataContext is PosApp.Desktop.ViewModels.SaleViewModel vm)
                {
                    vm.SelectProductCommand.Execute(item);
                }
            }
        }

        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Allow digits
            if (!string.IsNullOrEmpty(e.Text) && char.IsDigit(e.Text, 0))
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

        private void SaleView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Barcode Interceptor for Keyboard-Wedge Scanners
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                DateTime now = DateTime.Now;
                double elapsed = (now - _lastKeystrokeTime).TotalMilliseconds;
                _lastKeystrokeTime = now;

                if (elapsed < 50) // High speed = Scanner
                {
                    string keyStr = e.Key.ToString();
                    _barcodeBuffer.Append(keyStr[keyStr.Length - 1]);

                    // If we intercepted a fast key and focus is in CashReceivedBox, 
                    // we might need to clear the first digit that leaked in.
                    if (Keyboard.FocusedElement is TextBox tb && tb.Name == "CashReceivedBox")
                    {
                        // If buffer just started growing rapidly, it's a scan. 
                        // The first char is already in the box. 
                        // But we can't easily undo it without knowing if it was the first.
                        // For now, let's just capture the rest.
                    }
                    e.Handled = true;
                }
                else
                {
                    _barcodeBuffer.Clear();
                    string keyStr = e.Key.ToString();
                    _barcodeBuffer.Append(keyStr[keyStr.Length - 1]);
                }
            }
            else if (e.Key == Key.F1)
            {
                BarcodeBox.Focus();
                BarcodeBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.F2)
            {
                CashReceivedBox.Focus();
                CashReceivedBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                if (DataContext is PosApp.Desktop.ViewModels.SaleViewModel vm)
                {
                    // If we have a multiplier box or similar, focus it. 
                    // For now, let's just use it to focus quantity multiplier logic if we add a box.
                    // If not, maybe just focus the search? 
                    // Let's stick to F1/F2 for now as they are most critical.
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (DataContext is PosApp.Desktop.ViewModels.SaleViewModel vm)
                {
                    if (vm.ShowPaymentCarousel)
                    {
                        vm.ShowPaymentCarousel = false;
                        e.Handled = true;
                    }
                    else if (vm.HasSearchResults)
                    {
                        vm.SearchResults.Clear();
                        vm.HasSearchResults = false;
                        vm.ProductSearchText = "";
                        e.Handled = true;
                    }
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (_barcodeBuffer.Length > 3) 
                {
                    if (DataContext is PosApp.Desktop.ViewModels.SaleViewModel vm)
                    {
                        string bValue = _barcodeBuffer.ToString();
                        _barcodeBuffer.Clear();
                        
                        vm.Barcode = bValue;
                        vm.ProcessBarcodeCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }
                _barcodeBuffer.Clear();
            }

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (DataContext is PosApp.Desktop.ViewModels.SaleViewModel vm)
                {
                    if (e.Key == Key.D)
                    {
                        if (vm.DuplicateItemCommand.CanExecute(SaleGrid.SelectedItems))
                        {
                            vm.DuplicateItemCommand.Execute(SaleGrid.SelectedItems);
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == Key.C)
                    {
                        if (vm.CopyItemsCommand.CanExecute(SaleGrid.SelectedItems))
                        {
                            vm.CopyItemsCommand.Execute(SaleGrid.SelectedItems);
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == Key.V)
                    {
                        int insertIndex = SaleGrid.SelectedIndex >= 0 ? SaleGrid.SelectedIndex : 0;
                        if (vm.PasteItemsCommand.CanExecute(insertIndex))
                        {
                            vm.PasteItemsCommand.Execute(insertIndex);
                            e.Handled = true;
                        }
                    }
                }
            }
        }
        private void BarcodeBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is PosApp.Desktop.ViewModels.SaleViewModel vm)
            {
                if (vm.HasSearchResults)
                {
                    if (e.Key == Key.Down)
                    {
                        vm.SearchSelectedIndex = Math.Min(vm.SearchResults.Count - 1, vm.SearchSelectedIndex + 1);
                        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Up)
                    {
                        vm.SearchSelectedIndex = Math.Max(0, vm.SearchSelectedIndex - 1);
                        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Enter)
                    {
                        if (vm.SearchSelectedIndex >= 0 && vm.SearchSelectedIndex < vm.SearchResults.Count)
                        {
                            var selectedItem = vm.SearchResults[vm.SearchSelectedIndex];
                            vm.SelectProductCommand.Execute(selectedItem);
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
        }
    }
}
