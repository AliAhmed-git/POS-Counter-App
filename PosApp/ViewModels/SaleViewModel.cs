using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PosApp.Desktop.Models;
using PosApp.Desktop.Services;
using System.Linq;
using System.IO;
using PosApp.Desktop.Views;
using System.Threading;

namespace PosApp.Desktop.ViewModels
{

    public partial class SaleViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly IPrintService _printService;
        private readonly IScannerService _scannerService;
        private System.Threading.CancellationTokenSource? _searchCts;
        private SalesHead? _lastSale;
        private System.Collections.Generic.List<SalesDetail> _clipboardItems = new();
        private bool _isUpdatingTotals;

        public event Action? RequestFocus;
        public event Action? RequestSelectAll;

        [ObservableProperty]
        private string _barcode = string.Empty;

        [ObservableProperty]
        private string _customerName = "Walk-In";

        [ObservableProperty]
        private decimal _quantityMultiplier = 1.0m;

        [ObservableProperty]
        private SalesDetail? _lastAddedItem;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private Login? _currentUser;
        
        [ObservableProperty]
        private CounterInfo? _currentCounter;

        [ObservableProperty]
        private decimal _totalAmount;

        [ObservableProperty]
        private string _productSearchText = "";

        [ObservableProperty]
        private ObservableCollection<Item> _searchResults = new();

        [ObservableProperty]
        private decimal _totalTax;

        [ObservableProperty]
        private bool _showPaymentCarousel = false;

        [ObservableProperty]
        private decimal _cashReceived;

        [ObservableProperty]
        private decimal _balance;
        
        [ObservableProperty]
        private bool _isPaymentInsufficient;
        
        [ObservableProperty]
        private int _nextInvoiceNo;

        [ObservableProperty]
        private bool _hasSearchResults;

        [ObservableProperty]
        private string _searchSummary = "";

        [ObservableProperty]
        private int _searchSelectedIndex = -1;

        [ObservableProperty]
        private bool _isMultiplierLocked = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PaymentMethodCapitalized))]
        private string _selectedPaymentMethod = "Cash";

        [ObservableProperty]
        private decimal _serviceCharge;

        [ObservableProperty]
        private bool _isOnlinePayment;

        [ObservableProperty]
        private ObservableCollection<PaymentMethod> _paymentMethods = new();

        public string PaymentMethodCapitalized => SelectedPaymentMethod.ToUpper();

        [ObservableProperty]
        private string _balanceLabel = "CHANGE DUE";

        partial void OnIsOnlinePaymentChanged(bool value)
        {
            BalanceLabel = value ? "ONLINE/CARD" : "CHANGE DUE";
            UpdateTotals();
        }

        partial void OnSelectedPaymentMethodChanged(string value)
        {
            UpdateTotals();
        }

        public ObservableCollection<SalesDetail> SaleItems { get; } = new();
        
        [ObservableProperty]
        private decimal _invoiceDiscount;

        partial void OnInvoiceDiscountChanged(decimal value)
        {
            UpdateTotals();
        }

        private System.Collections.Generic.Dictionary<int, decimal> _itemUnitTaxes = new();

        public SaleViewModel(IDataService dataService, IPrintService printService, IScannerService scannerService)
        {
            _dataService = dataService;
            _printService = printService;
            _scannerService = scannerService;
            
            _scannerService.BarcodeScanned += OnSerialBarcodeScanned;
            _scannerService.Start();

            StatusText = "Loading system...";
            _ = InitializeAsync();
        }

        private void OnSerialBarcodeScanned(string barcode)
        {
            App.Current.Dispatcher.Invoke(async () => 
            {
                Barcode = barcode;
                await ProcessBarcode();
            });
        }

        private async Task InitializeAsync()
        {
            try
            {
                NextInvoiceNo = await _dataService.GetNextInvoiceNoAsync();
                
                // Load Payment Methods from DB
                try
                {
                    var methods = await _dataService.GetPaymentMethodsAsync();
                    foreach (var m in methods)
                    {
                        PaymentMethods.Add(m);
                    }

                    if (SelectedPaymentMethod == null && PaymentMethods.Any())
                    {
                        SelectedPaymentMethod = PaymentMethods[0].Method;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load payment methods: {ex.Message}");
                }

                StatusText = $"Ready. Bill #: {NextInvoiceNo}";
            }
            catch (Exception ex)
            {
                StatusText = "DB Error! Check connection.";
                System.Diagnostics.Debug.WriteLine($"Init failed: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SearchProducts()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ProductSearchText)) return;
                var results = await _dataService.SearchItemsAsync(ProductSearchText, "Item Name");
                SearchResults.Clear();
                foreach (var result in results) SearchResults.Add(result);
                HasSearchResults = SearchResults.Count > 0;
            }
            catch (Exception ex)
            {
                StatusText = "Search failed.";
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SelectProduct(Item item)
        {
            try
            {
                if (item == null) return;
                await AddItemToSaleAsync(item, QuantityMultiplier);
                if (!IsMultiplierLocked)
                {
                    QuantityMultiplier = 1.0m;
                }
                
                // Keep the search term so results stay open, BUT select all text 
                // so the user can immediately type a new search if they want.
                RequestSelectAll?.Invoke(); 
            }
            catch (Exception ex)
            {
                StatusText = "Error adding product.";
                System.Diagnostics.Debug.WriteLine($"Select error: {ex.Message}");
            }
        }

        // Helper method to add item to sale, used by both barcode and product selection
        public async Task AddItemToSaleAsync(Item item, decimal quantity = 1.0m)
        {
            var existingItem = SaleItems.FirstOrDefault(si => si.ItemCode == item.ItemCode && 
                string.Equals(si.Packing, item.Packing, StringComparison.OrdinalIgnoreCase));
            
            if (existingItem != null)
            {
                existingItem.Qty += quantity;
                LastAddedItem = existingItem;
                
                // Trigger Flash Effect
                existingItem.IsFlash = false;
                existingItem.IsFlash = true;
                _ = Task.Delay(100).ContinueWith(_ => App.Current.Dispatcher.Invoke(() => existingItem.IsFlash = false));

                // Move existing item to the top (index 0)
                int currentIndex = SaleItems.IndexOf(existingItem);
                if (currentIndex > 0)
                {
                    SaleItems.Move(currentIndex, 0);
                }
                
                UpdateTotals();
            }
            else
            {
                // Tax Calculation (Supporting both % and Absolute Amount)
                decimal taxValue = item.STax;
                decimal unitPriceInclusive = item.SPrice;
                decimal unitTax = 0;

                if (taxValue > 100)
                {
                    unitTax = taxValue;
                }
                else
                {
                    decimal unitPriceExclusive = unitPriceInclusive / (1 + (taxValue / 100));
                    unitTax = Math.Round(unitPriceInclusive - unitPriceExclusive, 2);
                }

                if (!_itemUnitTaxes.ContainsKey(item.ItemCode))
                {
                    _itemUnitTaxes[item.ItemCode] = taxValue;
                }

                var newItem = new SalesDetail
                {
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    Company = item.Company,
                    Packing = item.Packing,
                    LineNo = SaleItems.Any() ? SaleItems.Max(i => i.LineNo) + 1 : 1,
                    OriginalSPrice = unitPriceInclusive, // Store real price
                    SPrice = (item.RPrice > 0 && unitPriceInclusive > 0 && item.RPrice > unitPriceInclusive) ? item.RPrice : unitPriceInclusive,
                    PPrice = item.PPrice,
                    RPrice = item.RPrice,
                    Qty = quantity,
                    TaxAmount = Math.Round(unitTax * quantity, 2), // Keep 2 decimals for accuracy
                    Discount = (item.RPrice > 0 && unitPriceInclusive > 0 && item.RPrice > unitPriceInclusive) ? (item.RPrice - unitPriceInclusive) * quantity : 0m
                };

                LastAddedItem = newItem;

                // Subscribe to changes for real-time total updates
                newItem.PropertyChanged += OnItemPropertyChanged;

                // Fetch available packings
                var packings = await _dataService.GetPackingsForItemAsync(item.ItemCode);
                if (packings.Any())
                {
                    foreach (var p in packings)
                    {
                        newItem.AvailablePackings.Add(p);
                    }
                    
                    newItem.HasMultiplePackings = packings.Count > 1;

                    // Set current packing as selected (Case Insensitive)
                    var matchingPacking = packings.FirstOrDefault(p => p.PackingType != null && p.PackingType.Equals(item.Packing, StringComparison.OrdinalIgnoreCase));
                    newItem.SelectedPacking = matchingPacking ?? packings.FirstOrDefault();
                    
                    // IMPORTANT: Ensure the price is set from the selected packing
                    if (newItem.SelectedPacking != null)
                    {
                        newItem.SPrice = newItem.SelectedPacking.SPrice;
                        newItem.OriginalSPrice = newItem.SelectedPacking.SPrice;
                    }
                }
                
                // Initial calculation (Force update even if PropertyChanged didn't fire due to value equality)
                RecalculateItemTotal(newItem);
                
                SaleItems.Insert(0, newItem);

                // Trigger Flash Effect
                newItem.IsFlash = false;
                newItem.IsFlash = true;
                _ = Task.Delay(100).ContinueWith(_ => App.Current.Dispatcher.Invoke(() => newItem.IsFlash = false));
            }
            UpdateTotals();
        }

        private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is SalesDetail item)
            {
                if (e.PropertyName == nameof(SalesDetail.Qty))
                {
                    // VALIDATION: Prevent negative or zero quantity LOOPHOLE
                    if (item.Qty <= 0)
                    {
                        item.Qty = 1.0m;
                        StatusText = "WARNING: Quantity must be greater than zero!";
                        return;
                    }
                }

                if (e.PropertyName == nameof(SalesDetail.Qty) || 
                    e.PropertyName == nameof(SalesDetail.Discount) || 
                    e.PropertyName == nameof(SalesDetail.SPrice) || 
                    e.PropertyName == nameof(SalesDetail.SelectedPacking))
                {
                    // If packing changed, update tax amount
                    if (e.PropertyName == nameof(SalesDetail.SelectedPacking))
                    {
                        if (_itemUnitTaxes.TryGetValue(item.ItemCode, out decimal taxValue))
                        {
                            if (taxValue > 100)
                            {
                                item.TaxAmount = Math.Round(taxValue, 2);
                            }
                            else
                            {
                                decimal unitPriceExclusive = item.SPrice / (1 + (taxValue / 100));
                                item.TaxAmount = Math.Round(item.SPrice - unitPriceExclusive, 2);
                            }
                        }
                    }

                    // Re-apply RPrice logic if packing changed
                    if (e.PropertyName == nameof(SalesDetail.SelectedPacking))
                    {
                        if (item.SelectedPacking != null)
                        {
                            item.SPrice = item.SelectedPacking.SPrice;
                            item.OriginalSPrice = item.SelectedPacking.SPrice;
                            item.RPrice = item.SelectedPacking.RPrice;
                            item.PPrice = item.SelectedPacking.PPrice;
                        }

                        if (item.RPrice > 0 && item.OriginalSPrice > 0 && item.RPrice > item.OriginalSPrice)
                        {
                            item.SPrice = item.RPrice;
                        }
                    }



                    RecalculateItemTotal(item);
                    UpdateTotals();
                }
            }
        }

        private void RecalculateItemTotal(SalesDetail item)
        {


            // Automatic Discount Calculation: Discount = (RPrice - OriginalSPrice) * Qty
            if (item.RPrice > 0 && item.OriginalSPrice > 0 && item.RPrice > item.OriginalSPrice)
            {
                // Ensure SPrice is set to RPrice for display
                if (item.SPrice != item.RPrice) item.SPrice = item.RPrice;
                
                item.Discount = (item.RPrice - item.OriginalSPrice) * item.Qty;
            }

            // Re-calculate Total Item Tax based on new Qty
            decimal taxValue = 0m;
            if (_itemUnitTaxes.ContainsKey(item.ItemCode))
            {
                taxValue = _itemUnitTaxes[item.ItemCode];
            }
            
            decimal lineTotalInclusive = item.SPrice * item.Qty;
            decimal lineTax = 0m;

            if (taxValue > 100)
            {
                // Absolute Amount per unit
                lineTax = taxValue * item.Qty;
            }
            else
            {
                // Percentage based
                decimal lineTotalExclusive = lineTotalInclusive / (1 + (taxValue / 100));
                lineTax = lineTotalInclusive - lineTotalExclusive;
            }
            
            // USE PRECISION: No more Ceiling on line items to prevent cumulative errors
            item.TaxAmount = Math.Round(lineTax, 2);
            
            // ENSURE NetAmount is calculated precisely as (Price * Qty) - Discount
            item.NetAmount = Math.Round((item.SPrice * item.Qty) - item.Discount, 2);
        }

        [RelayCommand]
        private async Task ProcessBarcode()
        {
            if (string.IsNullOrWhiteSpace(Barcode)) return;

            IsProcessing = true;
            try
            {
                StatusText = "Looking up product...";
                var cleanedBarcode = Barcode.Trim();
                var item = await _dataService.GetItemByBarcodeAsync(cleanedBarcode);
                
                if (item == null)
                {
                    // Try searching by name if barcode lookup fails
                    var searchResults = await _dataService.SearchItemsAsync(cleanedBarcode, "Item Name");
                    item = searchResults.FirstOrDefault();
                }

                if (item != null)
                {
                    await AddItemToSaleAsync(item, QuantityMultiplier);
                    
                    StatusText = $"Added: {item.ItemName}";
                    Barcode = string.Empty; // Reset only on success
                    if (!IsMultiplierLocked)
                    {
                        QuantityMultiplier = 1.0m; // Reset multiplier only if unlocked
                    }
                    SearchResults.Clear();
                    HasSearchResults = false;
                }
                else
                {
                    StatusText = $"Product not found: {Barcode}";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Processing error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        partial void OnBarcodeChanged(string value)
        {
            _searchCts?.Cancel();
            _searchCts = new System.Threading.CancellationTokenSource();
            var token = _searchCts.Token;

            if (value.Length >= 2)
            {
                _ = Task.Run(async () => 
                {
                    try 
                    {
                        await Task.Delay(300, token);
                        await UpdateSearchResultsAsync(value);
                    }
                    catch (OperationCanceledException) { }
                }, token);
            }
            else
            {
                SearchResults.Clear();
                HasSearchResults = false;
            }
        }

        private async Task UpdateSearchResultsAsync(string query)
        {
            try
            {
                // request a very large number to effectively remove the limit
                var results = await _dataService.SearchItemsAsync(query, "Item Name", 0, 50000); 
                
                await App.Current.Dispatcher.InvokeAsync(() => {
                    SearchResults.Clear();
                    foreach (var res in results)
                    {
                        SearchResults.Add(res);
                    }
                    HasSearchResults = SearchResults.Count > 0;
                    SearchSummary = $"Found {SearchResults.Count} items.";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSearchResultsAsync failed: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SelectPaymentMethod(object? parameter)
        {
            if (parameter is PaymentMethod method)
            {
                SelectedPaymentMethod = method.Method;
                bool isCash = string.Equals(SelectedPaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase);
                IsOnlinePayment = !isCash;
                
                if (!isCash)
                {
                    _lastSelectedOnlineMethod = SelectedPaymentMethod;
                }
                
                foreach (var pm in PaymentMethods) pm.IsSelected = (pm == method);
                
                OnPropertyChanged(nameof(PaymentMethodCapitalized));
                UpdateTotals();
                ShowPaymentCarousel = false; // Hide after selection
                StatusText = $"Payment Method: {SelectedPaymentMethod}";
            }
            else
            {
                // Toggle carousel visibility instead of opening dialog immediately
                ShowPaymentCarousel = !ShowPaymentCarousel;
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task PrintReceipt()
        {
            try
            {
                if (SaleItems.Count == 0) return;
                
                if (IsPaymentInsufficient)
                {
                    StatusText = "CANNOT PRINT: DEMAND CASH - Payment is insufficient!";
                    return;
                }

                if (IsProcessing) return;
                IsProcessing = true;

                StatusText = "Finalizing sale...";

                // Auto-merge items before printing
                PerformMerging();

                // Get the absolute latest invoice number to prevent collisions
                NextInvoiceNo = await _dataService.GetNextInvoiceNoAsync();

                var sale = new SalesHead
                {
                    Date = DateTime.Now,
                    Details = SaleItems.ToList(),
                    CashPaid = CashReceived,
                    TotalAmount = TotalAmount + ServiceCharge, // Include online charges in total bill
                    InvoiceNo = NextInvoiceNo,
                    User = CurrentUser?.User,
                    CustomerName = CustomerName,
                    InvoiceDiscount = InvoiceDiscount,
                    // Added back for Card/Wallet payments and Refund state
                    PaymentMethod = (IsOnlinePayment) ? SelectedPaymentMethod : "Cash",
                    ServiceCharge = (IsOnlinePayment) ? ServiceCharge : 0,
                    CardPaid = (IsOnlinePayment) ? Math.Max(0, (TotalAmount + ServiceCharge) - CashReceived) : 0
                };

                // Save to database
                bool saved = await _dataService.ProcessSaleAsync(sale);
                if (!saved)
                {
                    StatusText = "Error saving sale to database! Please try again.";
                    return;
                }

                await _printService.PrintReceiptAsync(sale);
                
                // Save state for "Prev" button before clearing
                _lastSale = new SalesHead
                {
                    InvoiceNo = sale.InvoiceNo,
                    Date = sale.Date,
                    CustomerName = sale.CustomerName,
                    CashPaid = sale.CashPaid,
                    TotalAmount = sale.TotalAmount,
                    Details = sale.Details?.Select(d => new SalesDetail
                    {
                        ItemCode = d.ItemCode,
                        ItemName = d.ItemName,
                        Company = d.Company,
                        Packing = d.Packing,
                        Qty = d.Qty,
                        SPrice = d.SPrice,
                        TaxAmount = d.TaxAmount,
                        Discount = d.Discount,
                        NetAmount = d.NetAmount,
                        // Preserve all metadata for recovery
                        SelectedPacking = d.SelectedPacking,
                        HasMultiplePackings = d.HasMultiplePackings,
                        PPrice = d.PPrice,
                        RPrice = d.RPrice
                    }).ToList() ?? new List<SalesDetail>()
                };

                // Manually populate read-only AvailablePackings for each Detail in _lastSale
                if (sale.Details != null && _lastSale.Details != null)
                {
                    var sDetails = sale.Details.ToList();
                    var lDetails = _lastSale.Details.ToList();
                    for (int i = 0; i < sDetails.Count && i < lDetails.Count; i++)
                    {
                        foreach (var p in sDetails[i].AvailablePackings)
                        {
                            lDetails[i].AvailablePackings.Add(p);
                        }
                    }
                }
                
                // Auto-void sale after print to prepare for new customer
                SaleItems.Clear();
                CashReceived = 0;
                CustomerName = "Walk-In";
                IsOnlinePayment = false;
                SelectedPaymentMethod = "Cash";
                ServiceCharge = 0;
                InvoiceDiscount = 0;
                UpdateTotals();
                
                // Get the next invoice number for the next customer
                NextInvoiceNo = await _dataService.GetNextInvoiceNoAsync();
                StatusText = $"Sale #{sale.InvoiceNo} completed. Next: {NextInvoiceNo}";
                
                // Return focus to Cash Received field
                // Return focus to Cash Received field
                RequestFocus?.Invoke();
            }
            catch (Exception ex)
            {
                string shortError = ex.Message.Length > 50 ? ex.Message.Substring(0, 50) + "..." : ex.Message;
                StatusText = $"Checkout FAILED: {shortError}";
                System.Diagnostics.Debug.WriteLine($"PrintReceipt Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task PrintLastReceipt()
        {
            SalesHead? lastSale = await _dataService.GetLastSaleAsync();
            string lastInvNo = lastSale?.InvoiceNo.ToString() ?? "";

            var dialog = new LastSaleDialog(lastInvNo);
            SafelySetDialogOwner(dialog);
            
            if (dialog.ShowDialog() == true)
            {
                if (!int.TryParse(dialog.InvoiceNo, out int invNo))
                {
                    StatusText = "Invalid invoice number.";
                    return;
                }

                StatusText = $"Fetching bill #{invNo}...";
                var sale = await _dataService.GetSaleByInvoiceAsync(invNo);

                if (sale == null)
                {
                    StatusText = "Sale not found.";
                    return;
                }

                if (dialog.Result == LastSaleDialog.LastSaleResult.Print)
                {
                    StatusText = $"Printing bill #{sale.InvoiceNo}...";
                    await _printService.PrintReceiptAsync(sale);
                }
                else if (dialog.Result == LastSaleDialog.LastSaleResult.Add)
                {
                    // Clear current sale first as per user request
                    ClearSale();
                    await AddSaleItemsToCurrent(sale);
                    StatusText = $"Items from #{sale.InvoiceNo} added to current bill.";
                }
            }
        }

        private async Task AddSaleItemsToCurrent(SalesHead sale)
        {
            if (sale.Details == null) return;
            foreach (var detail in sale.Details)
            {
                var newItem = new SalesDetail
                {
                    ItemCode = detail.ItemCode,
                    ItemName = detail.ItemName,
                    Company = detail.Company,
                    Packing = detail.Packing,
                    SPrice = detail.SPrice,
                    PPrice = detail.PPrice,
                    RPrice = detail.RPrice,
                    Qty = detail.Qty,
                    TaxAmount = detail.TaxAmount,
                    Discount = detail.Discount,
                    NetAmount = detail.NetAmount,
                    HasMultiplePackings = false,
                    OriginalSPrice = (detail.Qty > 0 && detail.Discount > 0) ? (detail.SPrice - (detail.Discount / detail.Qty)) : detail.SPrice
                };

                var fullItem = await _dataService.GetItemByBarcodeAsync(detail.ItemCode.ToString());
                if (fullItem != null)
                {
                    var packings = await _dataService.GetPackingsForItemAsync(fullItem.ItemCode);
                    foreach (var p in packings) newItem.AvailablePackings.Add(p);
                    newItem.HasMultiplePackings = packings.Count > 1;
                    newItem.SelectedPacking = packings.FirstOrDefault(p => p.PackingType == detail.Packing);
                }
                
                newItem.PropertyChanged += OnItemPropertyChanged;
                SaleItems.Add(newItem);
            }
            UpdateTotals();
        }

        [RelayCommand]
        private async Task PrintTestBarcodes()
        {
            StatusText = "Fetching random barcodes for testing...";
            var packings = await _dataService.GetRandomPackingsAsync(10);
            if (packings == null || !packings.Any())
            {
                StatusText = "No barcodes found in database.";
                return;
            }

            StatusText = "Printing test barcode sheet...";
            await _printService.PrintBarcodeTestSheetAsync(packings);
            StatusText = "Test barcode sheet printed.";
        }

        [RelayCommand]
        private async Task RecoverLastBill()
        {

            var inputDialog = new TextInputDialog("RECOVER INVOICE NO", "");
            SafelySetDialogOwner(inputDialog);

            SalesHead? saleToRecover = null;
            if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.Result))
            {
                if (int.TryParse(inputDialog.Result, out int invNo))
                {
                    StatusText = $"Fetching bill #{invNo} to recover...";
                    saleToRecover = await _dataService.GetSaleByInvoiceAsync(invNo);
                }
            }
            else if (inputDialog.DialogResult == true)
            {
                saleToRecover = _lastSale;
            }
            else
            {
                return;
            }

            if (saleToRecover == null || saleToRecover.Details == null)
            {
                StatusText = "No bill found to recover.";
                return;
            }

            SaleItems.Clear();
            _itemUnitTaxes.Clear();

            foreach (var detail in saleToRecover.Details)
            {
                var recoveredItem = new SalesDetail
                {
                    ItemCode = detail.ItemCode,
                    ItemName = detail.ItemName,
                    Company = detail.Company,
                    Packing = detail.Packing,
                    SPrice = detail.SPrice,
                    PPrice = detail.PPrice,
                    RPrice = detail.RPrice,
                    Qty = detail.Qty,
                    TaxAmount = detail.TaxAmount,
                    Discount = detail.Discount,
                    NetAmount = detail.NetAmount,
                    HasMultiplePackings = false, // Will be updated by AddItemToSaleAsync style logic if we were using it, but here we just restore
                    OriginalSPrice = (detail.Qty > 0 && detail.Discount > 0) ? (detail.SPrice - (detail.Discount / detail.Qty)) : detail.SPrice
                };

                // Try to find full item info to restore packings
                var fullItem = await _dataService.GetItemByBarcodeAsync(detail.ItemCode.ToString());
                if (fullItem != null)
                {
                    var packings = await _dataService.GetPackingsForItemAsync(fullItem.ItemCode);
                    foreach (var p in packings) recoveredItem.AvailablePackings.Add(p);
                    recoveredItem.HasMultiplePackings = packings.Count > 1;
                    recoveredItem.SelectedPacking = packings.FirstOrDefault(p => p.PackingType == detail.Packing);
                }
                
                recoveredItem.PropertyChanged += OnItemPropertyChanged;
                SaleItems.Add(recoveredItem);
            }

            CashReceived = saleToRecover.CashPaid;
            CustomerName = saleToRecover.CustomerName ?? "Walk-In";
            
            // Restore payment method if possible
            if (!string.IsNullOrEmpty(saleToRecover.PaymentMethod) && !string.Equals(saleToRecover.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase))
            {
                SelectedPaymentMethod = saleToRecover.PaymentMethod;
                IsOnlinePayment = true;
                ServiceCharge = saleToRecover.ServiceCharge;
            }
            else
            {
                SelectedPaymentMethod = "Cash";
                IsOnlinePayment = false;
                ServiceCharge = 0;
            }

            UpdateTotals();
            StatusText = $"Recovered Bill #{saleToRecover.InvoiceNo}.";
        }

        [RelayCommand]
        private void MergeItems()
        {
            PerformMerging();
            StatusText = "Items merged by packing.";
        }

        private void PerformMerging()
        {
            if (SaleItems.Count <= 1) return;

            var itemsToMerge = SaleItems.ToList();
            var grouped = itemsToMerge.GroupBy(i => new { i.ItemCode, i.Packing });

            bool changed = false;
            foreach (var group in grouped)
            {
                if (group.Count() > 1)
                {
                    var firstItem = group.First();
                    decimal totalQty = group.Sum(i => i.Qty);
                    
                    // Update first item with total quantity
                    firstItem.Qty = totalQty;
                    
                    // Remove others from SaleItems
                    foreach (var other in group.Skip(1))
                    {
                        SaleItems.Remove(other);
                    }
                    changed = true;
                }
            }

            if (changed)
            {
                UpdateTotals();
            }
        }

        [RelayCommand]
        private void SelectAllItems()
        {
            RequestSelectAll?.Invoke();
        }

        [RelayCommand]
        private void DuplicateItem(object parameter)
        {
            if (parameter == null) return;
            
            // Handle both single item (direct click) and multiple items (SelectedItems)
            var itemsToDuplicate = new System.Collections.Generic.List<SalesDetail>();
            if (parameter is System.Collections.IList list)
            {
                foreach (var item in list.OfType<SalesDetail>()) itemsToDuplicate.Add(item);
            }
            else if (parameter is SalesDetail singleItem)
            {
                itemsToDuplicate.Add(singleItem);
            }

            if (!itemsToDuplicate.Any()) return;

            // Sort by index descending to handle insertions without messing up subsequent indices
            var indexedItems = itemsToDuplicate
                .Select(item => new { Item = item, Index = SaleItems.IndexOf(item) })
                .OrderByDescending(x => x.Index)
                .ToList();

            foreach (var mapping in indexedItems)
            {
                var item = mapping.Item;
                var newItem = new SalesDetail
                {
                    InvoiceNo = item.InvoiceNo,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    Company = item.Company,
                    Packing = item.Packing,
                    LineNo = SaleItems.Any() ? SaleItems.Max(i => i.LineNo) + 1 : 1,
                    SPrice = item.SPrice,
                    PPrice = item.PPrice,
                    RPrice = item.RPrice,
                    Qty = 1,
                    TaxAmount = item.TaxAmount / (item.Qty > 0 ? item.Qty : 1),
                    Discount = 0,
                    HasMultiplePackings = item.HasMultiplePackings
                };

                foreach (var p in item.AvailablePackings) newItem.AvailablePackings.Add(p);
                newItem.SelectedPacking = item.SelectedPacking;
                newItem.PropertyChanged += OnItemPropertyChanged;
                
                RecalculateItemTotal(newItem);
                
                // Insert directly after the original item
                SaleItems.Insert(mapping.Index + 1, newItem);
            }
            
            UpdateTotals();
            StatusText = $"Duplicated {itemsToDuplicate.Count} item(s).";
        }

        [RelayCommand]
        private void CopyItems(object parameter)
        {
            if (parameter == null) return;

            var itemsToCopy = new System.Collections.Generic.List<SalesDetail>();
            if (parameter is System.Collections.IList list)
            {
                foreach (var item in list.OfType<SalesDetail>()) itemsToCopy.Add(item);
            }
            else if (parameter is SalesDetail singleItem)
            {
                itemsToCopy.Add(singleItem);
            }

            if (!itemsToCopy.Any()) return;

            _clipboardItems.Clear();
            foreach (var item in itemsToCopy)
            {
                var clone = new SalesDetail
                {
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    Company = item.Company,
                    Packing = item.Packing,
                    SPrice = item.SPrice,
                    PPrice = item.PPrice,
                    RPrice = item.RPrice,
                    Qty = item.Qty,
                    TaxAmount = item.TaxAmount,
                    Discount = item.Discount,
                    NetAmount = item.NetAmount,
                    HasMultiplePackings = item.HasMultiplePackings,
                    SelectedPacking = item.SelectedPacking
                };
                foreach (var p in item.AvailablePackings) clone.AvailablePackings.Add(p);
                _clipboardItems.Add(clone);
            }

            StatusText = $"Copied {itemsToCopy.Count} item(s) to clipboard.";
        }

        [RelayCommand]
        private void PasteItems(object parameter)
        {
            if (!_clipboardItems.Any())
            {
                StatusText = "Clipboard is empty.";
                return;
            }

            int insertIndex = 0;
            if (parameter is int index && index >= 0 && index <= SaleItems.Count)
            {
                insertIndex = index;
            }

            foreach (var item in _clipboardItems)
            {
                var newItem = new SalesDetail
                {
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    Company = item.Company,
                    Packing = item.Packing,
                    LineNo = SaleItems.Any() ? SaleItems.Max(i => i.LineNo) + 1 : 1,
                    SPrice = item.SPrice,
                    PPrice = item.PPrice,
                    RPrice = item.RPrice,
                    Qty = item.Qty,
                    TaxAmount = item.TaxAmount,
                    Discount = item.Discount,
                    HasMultiplePackings = item.HasMultiplePackings,
                    SelectedPacking = item.SelectedPacking
                };

                foreach (var p in item.AvailablePackings) newItem.AvailablePackings.Add(p);
                newItem.PropertyChanged += OnItemPropertyChanged;
                
                RecalculateItemTotal(newItem);
                SaleItems.Insert(insertIndex++, newItem);
            }

            UpdateTotals();
            StatusText = $"Pasted {_clipboardItems.Count} item(s).";
        }

        [RelayCommand]
        private void IncrementQty(SalesDetail item)
        {
            if (item == null) return;
            item.Qty += 1;
            RecalculateItemTotal(item);
            UpdateTotals();
        }

        [RelayCommand]
        private void DecrementQty(SalesDetail item)
        {
            if (item == null || item.Qty <= 1) return;
            item.Qty -= 1;
            RecalculateItemTotal(item);
            UpdateTotals();
        }

        [RelayCommand]
        private void ClearBarcode()
        {
            Barcode = string.Empty;
            RequestFocus?.Invoke();
        }


        [RelayCommand]
        private void EditWeightQuantity(SalesDetail item)
        {
            if (item == null || !item.IsWeightUnit) return;

            var dialog = new QuantityInputDialog(item.Qty);
            SafelySetDialogOwner(dialog);
            
            if (dialog.ShowDialog() == true && dialog.IsConfirmed)
            {
                item.Qty = dialog.Quantity;
                RecalculateItemTotal(item);
                UpdateTotals();
                StatusText = $"Updated quantity for {item.ItemName} to {item.Qty}";
            }
        }

        [RelayCommand]
        private void RemoveItem(object parameter)
        {
            if (parameter == null) return;

            var itemsToRemove = new System.Collections.Generic.List<SalesDetail>();
            if (parameter is System.Collections.IList list)
            {
                foreach (var item in list.OfType<SalesDetail>()) itemsToRemove.Add(item);
            }
            else if (parameter is SalesDetail singleItem)
            {
                itemsToRemove.Add(singleItem);
            }

            if (!itemsToRemove.Any()) return;

            if (!itemsToRemove.Any()) return;
            
            string supervisorKey = CurrentCounter?.SupervisorKey ?? "123";
            string userKey = CurrentUser?.Password ?? "123";
            var dialog = new PasswordDialog(new[] { userKey, supervisorKey }, "CONFIRM DELETE");
            SafelySetDialogOwner(dialog);
            bool? result = dialog.ShowDialog();

            if (result == true || dialog.IsVerified)
            {
                foreach (var item in itemsToRemove)
                {
                    LogVoidAction("REMOVE_ITEM", item);
                    SaleItems.Remove(item);
                }
                UpdateTotals();
                StatusText = $"Removed {itemsToRemove.Count} item(s).";
            }
        }

        [RelayCommand]
        private void VoidSale()
        {
            if (SaleItems.Count == 0) return;

            // Password Protection for Voiding Sale
            // Password Protection for Voiding Sale
            string supervisorKey = CurrentCounter?.SupervisorKey ?? "123";
            string userKey = CurrentUser?.Password ?? "123";
            var passwordDialog = new PasswordDialog(new[] { userKey, supervisorKey }, "CONFIRM VOID");
            if (passwordDialog.ShowDialog() == true)
            {
                ClearSale();
                StatusText = "Sale voided.";
            }
        }

        [RelayCommand]
        private void ToggleMultiplierLock()
        {
            IsMultiplierLocked = !IsMultiplierLocked;
            StatusText = IsMultiplierLocked ? "Multiplier LOCKED" : "Multiplier UNLOCKED";
        }

        [RelayCommand]
        private void ClearSale()
        {
            SaleItems.Clear();
            _itemUnitTaxes.Clear();
            CashReceived = 0;
            SelectedPaymentMethod = "Cash";
            IsOnlinePayment = false;
            ServiceCharge = 0;
            InvoiceDiscount = 0;
            CustomerName = "Walk-In";
            
            foreach (var pm in PaymentMethods) pm.IsSelected = (pm.Method == "Cash");
            
            UpdateTotals();
            StatusText = "Sale cleared.";
        }

        private void LogVoidAction(string actionType, SalesDetail item)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] | USER: {CurrentUser?.User ?? "Unknown"} | ACTION: {actionType} | ITEM: {item.ItemName} ({item.ItemCode}) | PACKING: {item.Packing} | QTY: {item.Qty} | PRICE: {item.SPrice} | TOTAL: {item.NetAmount}\n";
                File.AppendAllText("voided_items.txt", logEntry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log void action: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ReturnItems()
        {
            // Simplified workflow: Just open dialog for invoice entry and item selection
            // The dialog handles the complete refund process internally
            
            var loadDialog = new TextInputDialog("LOAD INVOICE FOR RETURN", "");
            SafelySetDialogOwner(loadDialog);
            
            if (loadDialog.ShowDialog() == true && int.TryParse(loadDialog.Result, out int invToLoad))
            {
                StatusText = $"Loading Invoice {invToLoad}...";
                var original = await _dataService.GetSaleByInvoiceAsync(invToLoad);
                
                if (original != null && original.Details != null && original.Details.Any())
                {
                    // Open RefundDialog - it handles everything internally
                    var refundDialogViewModel = new RefundDialogViewModel(
                        original, 
                        _dataService, 
                        _printService, 
                        CurrentUser, 
                        CurrentCounter
                    );
                    var refundDialog = new RefundDialog(refundDialogViewModel);
                    SafelySetDialogOwner(refundDialog);
                    
                    if (refundDialog.ShowDialog() == true)
                    {
                        StatusText = $"Refund for Invoice {invToLoad} completed successfully.";
                    }
                    else
                    {
                        StatusText = "Refund cancelled.";
                    }
                }
                else
                {
                    StatusText = "Invoice not found or has no items.";
                }
            }
        }

        [RelayCommand]
        private async Task SelectShiftItems()
        {
            var dialog = new QuantityInputDialog(0);
            dialog.Title = "Enter Bill No to Load";
            SafelySetDialogOwner(dialog);
            
            if (dialog.ShowDialog() == true && dialog.IsConfirmed && dialog.Quantity > 0)
            {
                int invoiceNo = (int)dialog.Quantity;
                var oldSale = await _dataService.GetSaleByInvoiceAsync(invoiceNo);
                if (oldSale != null && oldSale.Details != null)
                {
                    foreach (var detail in oldSale.Details)
                    {
                        var newItem = new SalesDetail
                        {
                            InvoiceNo = 0,
                            ItemCode = detail.ItemCode,
                            ItemName = detail.ItemName,
                            Company = detail.Company,
                            Packing = detail.Packing,
                            LineNo = SaleItems.Any() ? SaleItems.Max(i => i.LineNo) + 1 : 1,
                            SPrice = detail.SPrice,
                            PPrice = detail.PPrice,
                            RPrice = detail.RPrice,
                            Qty = detail.Qty,
                            Discount = detail.Discount,
                            TaxAmount = detail.TaxAmount,
                            NetAmount = detail.NetAmount
                        };
                        newItem.PropertyChanged += OnItemPropertyChanged;
                        SaleItems.Add(newItem);
                    }
                    UpdateTotals();
                    StatusText = $"Loaded {oldSale.Details.Count} items from Bill #{invoiceNo}";
                }
                else
                {
                    StatusText = $"Bill #{invoiceNo} not found.";
                }
            }
        }


        [ObservableProperty]
        private decimal _totalItems;

        private string _lastSelectedOnlineMethod = "JazzCash"; // Default startup online choice

        private void UpdateTotals()
        {
            if (_isUpdatingTotals) return;
            _isUpdatingTotals = true;
            
            try
            {
                // USE PRECISION: Calculate exact totals without Ceiling to avoid "Math Errors"
                decimal subTotal = Math.Round(SaleItems.Sum(i => i.NetAmount), 2);
                TotalAmount = Math.Round(subTotal - InvoiceDiscount, 2);
                if (TotalAmount < 0) TotalAmount = 0;

                // STICKY LOGIC: Handle switching between Cash and Online automatically
                if (TotalAmount > 0 && CashReceived >= TotalAmount)
                {
                    // If cash is enough, we shift to Cash mode but DON'T forget the last online method
                    IsOnlinePayment = false;
                    SelectedPaymentMethod = "Cash";
                    ServiceCharge = 0;
                    
                    BalanceLabel = "CHANGE DUE";
                    foreach (var pm in PaymentMethods) pm.IsSelected = (pm.Method == "Cash");
                    OnPropertyChanged(nameof(PaymentMethodCapitalized));
                }
                else if (TotalAmount > 0 && CashReceived < TotalAmount && !IsOnlinePayment && !string.IsNullOrEmpty(_lastSelectedOnlineMethod))
                {
                    // DISABLED: Auto-switch to online when cash insufficient - user must manually select payment method
                    /*
                    SelectedPaymentMethod = _lastSelectedOnlineMethod;
                    IsOnlinePayment = true;
                    BalanceLabel = "ONLINE/CARD";
                    foreach (var pm in PaymentMethods) pm.IsSelected = (pm.Method == _lastSelectedOnlineMethod);
                    OnPropertyChanged(nameof(PaymentMethodCapitalized));
                    */
                }

                if (IsOnlinePayment)
                {
                    // REAL-TIME: Recalculate Service Charge if online payment is active
                    // USE PRECISION: Calculate based on the amount actually being paid online (TotalAmount - CashReceived)
                    var method = PaymentMethods.FirstOrDefault(pm => string.Equals(pm.Method, SelectedPaymentMethod, StringComparison.OrdinalIgnoreCase));
                    if (method != null)
                    {
                        decimal cardAmount = Math.Max(0, TotalAmount - CashReceived);
                        decimal charges = (cardAmount * method.ChargePercentage / 100);
                        decimal taxOnCharges = (charges * method.TaxPercentage / 100);
                        ServiceCharge = Math.Round(charges + taxOnCharges, 0);
                    }
                }
                else
                {
                    ServiceCharge = 0;
                }
                
                TotalTax = Math.Round(SaleItems.Sum(i => i.TaxAmount), 2);
                TotalItems = SaleItems.Sum(i => i.Qty);
                
                if (IsOnlinePayment)
                {
                    // For online payment, Balance is the amount to be paid on card/wallet
                    decimal totalToPay = TotalAmount + ServiceCharge;
                    Balance = Math.Max(0, totalToPay - CashReceived);
                    IsPaymentInsufficient = false; // Usually online handles the rest
                }
                else
                {
                    // For cash payment, Balance is the change due to the customer
                    Balance = (CashReceived > 0) ? (CashReceived - TotalAmount) : 0m;
                    IsPaymentInsufficient = TotalAmount > 0 && CashReceived < TotalAmount;
                }
                
                if (IsPaymentInsufficient && !IsOnlinePayment)
                {
                    StatusText = "DEMAND CASH: Received amount is less than total!";
                }
                else if (TotalAmount > 0)
                {
                    StatusText = $"Ready. Bill #: {NextInvoiceNo}";
                }
            }
            finally
            {
                _isUpdatingTotals = false;
            }
        }

        partial void OnCashReceivedChanged(decimal value)
        {
            UpdateTotals();
        }
    }   
}