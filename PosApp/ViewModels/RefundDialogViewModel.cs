using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PosApp.Desktop.Models;
using PosApp.Desktop.Services;
using System.Threading.Tasks;

namespace PosApp.Desktop.ViewModels
{
    public partial class RefundDialogViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IPrintService _printService;
        private readonly Login? _currentUser;
        private readonly CounterInfo? _currentCounter;
        private readonly int _originalInvoiceNo;

        [ObservableProperty]
        private string _headerText;

        [ObservableProperty]
        private string _dateText;

        [ObservableProperty]
        private bool _showAgeWarning;

        [ObservableProperty]
        private string _ageWarningText;

        [ObservableProperty]
        private decimal _refundTotal;

        [ObservableProperty]
        private bool _hasSelectedItems;

        public ObservableCollection<RefundItem> Items { get; } = new();

        public RefundDialogViewModel(SalesHead originalSale, IDataService dataService, IPrintService printService, Login? currentUser, CounterInfo? currentCounter)
        {
            if (originalSale == null)
                throw new ArgumentNullException(nameof(originalSale));

            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _printService = printService ?? throw new ArgumentNullException(nameof(printService));
            _currentUser = currentUser;
            _currentCounter = currentCounter;
            _originalInvoiceNo = originalSale.InvoiceNo;

            HeaderText = $"Refund Invoice #{originalSale.InvoiceNo}";
            DateText = $"Date: {originalSale.Date:yyyy-MM-dd HH:mm:ss}";

            // Calculate age and show warning if > 2 days
            var age = DateTime.Now - originalSale.Date;
            ShowAgeWarning = age.TotalDays > 2;
            AgeWarningText = ShowAgeWarning 
                ? $"⚠ WARNING: This receipt is {age.Days} days old!" 
                : string.Empty;

            // Load items
            if (originalSale.Details != null)
            {
                foreach (var detail in originalSale.Details)
                {
                    var refundItem = new RefundItem
                    {
                        ItemCode = detail.ItemCode,
                        ItemName = detail.ItemName ?? string.Empty,
                        Packing = detail.Packing ?? string.Empty,
                        Qty = detail.Qty,
                        SPrice = detail.SPrice,
                        NetAmount = detail.NetAmount,
                        Discount = detail.Discount,
                        TaxAmount = detail.TaxAmount,
                        IsSelected = true // All items selected by default
                    };

                    refundItem.PropertyChanged += RefundItem_PropertyChanged;
                    Items.Add(refundItem);
                }
            }

            CalculateRefundTotal();
        }

        private void RefundItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RefundItem.IsSelected))
            {
                CalculateRefundTotal();
            }
        }

        private void CalculateRefundTotal()
        {
            RefundTotal = Items.Where(i => i.IsSelected).Sum(i => i.NetAmount);
            HasSelectedItems = Items.Any(i => i.IsSelected);
        }

        public ObservableCollection<SalesDetail> GetSelectedItems()
        {
            var selectedItems = new ObservableCollection<SalesDetail>();
            foreach (var item in Items.Where(i => i.IsSelected))
            {
                selectedItems.Add(new SalesDetail
                {
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName ?? string.Empty,
                    Packing = item.Packing ?? string.Empty,
                    Qty = item.Qty,
                    SPrice = item.SPrice,
                    NetAmount = item.NetAmount,
                    Discount = item.Discount,
                    TaxAmount = item.TaxAmount
                });
            }
            return selectedItems;
        }

        public async Task<bool> ProcessRefundAsync()
        {
            try
            {
                // Delete original invoice from database and archives
                await _dataService.DeleteSaleAsync(_originalInvoiceNo);
                await _printService.DeleteArchiveEntryAsync(_originalInvoiceNo);

                // Create refund record using selected items
                var selectedItems = GetSelectedItems();
                var refundSale = new SalesHead
                {
                    InvoiceNo = _originalInvoiceNo, // Reuse original ID
                    Date = DateTime.Now,
                    Details = selectedItems.ToList(),
                    CashPaid = RefundTotal,
                    TotalAmount = RefundTotal,
                    User = _currentUser?.User ?? "Admin",
                    CounterNo = _currentCounter?.CounterNo ?? 1,
                    CustomerName = $"REFUND (Orig Inv:{_originalInvoiceNo})",
                    IsRefund = true,
                    PaymentMethod = "Cash"
                };

                // Save refund to database
                await _dataService.ProcessSaleAsync(refundSale);

                // Print refund receipt
                await _printService.PrintRefundReceiptAsync(refundSale);

                // Log refund to audit file
                string auditEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] | USER: {_currentUser?.User ?? "Unknown"} | REFUND INVOICE: {_originalInvoiceNo} | AMOUNT: {RefundTotal} | ITEMS: {selectedItems.Count}\n";
                System.IO.File.AppendAllText("refund_audit.txt", auditEntry);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessRefund Error: {ex.Message}");
                return false;
            }
        }
    }

    public partial class RefundItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        public int ItemCode { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Packing { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal SPrice { get; set; }
        public decimal NetAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal TaxAmount { get; set; }
    }
}
