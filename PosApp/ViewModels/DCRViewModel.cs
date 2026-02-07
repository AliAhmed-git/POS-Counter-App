using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PosApp.Desktop.Services;
using PosApp.Desktop.Views;

namespace PosApp.Desktop.ViewModels
{
    public partial class DCRViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly IPrintService _printService;

        [ObservableProperty]
        private string _userName = "Admin";

        [ObservableProperty]
        private int _counterNo = 1;

        public DCRViewModel(IDataService dataService, IPrintService printService)
        {
            _dataService = dataService;
            _printService = printService;
            StatusText = "DCR Ready";
        }


        [RelayCommand]
        public async Task OpenDcr()
        {
            try
            {
                var dialog = new DenominationDialog();
                SafelySetDialogOwner(dialog);
                
                if (dialog.ShowDialog() == true)
            {
                var rawSales = await _dataService.GetSalesForDCRAsync(DateTime.Today);
                // Ensure no duplicates exist (safety check against double-saving or join issues)
                var sales = rawSales.GroupBy(s => s.InvoiceNo).Select(g => g.First()).ToList();
                
                var methods = sales.GroupBy(s => s.PaymentMethod ?? "Cash")
                                  .Select(g => new DCRMethodSummary 
                                  { 
                                      Method = g.Key, 
                                      Total = g.Sum(s => s.TotalAmount),
                                      Fee = g.Sum(s => s.ServiceCharge)
                                  })
                                  .ToList();

                var totalSales = sales.Where(s => !s.IsRefund).Sum(s => s.TotalAmount + s.InvoiceDiscount);
                var totalCashSales = sales.Where(s => !s.IsRefund).Sum(s => s.CashPaid);
                var totalOnlineSales = sales.Where(s => !s.IsRefund).Sum(s => s.CardPaid);

                var totalRefunds = sales.Where(s => s.IsRefund).Sum(s => s.TotalAmount);
                var totalDiscount = sales.Where(s => !s.IsRefund).Sum(s => s.InvoiceDiscount + (s.Details?.Sum(d => d.Discount) ?? 0));
                var salesCount = sales.Count(s => !s.IsRefund);
                var refundCount = sales.Count(s => s.IsRefund);
                var totalItemsSold = sales.Where(s => !s.IsRefund).Sum(s => s.Details?.Sum(d => d.Qty) ?? 0);
                var totalRefundedItems = sales.Where(s => s.IsRefund).Sum(s => s.Details?.Sum(d => d.Qty) ?? 0);

                // Calculate expected cash (cash payments only, excluding refunds)
                var expectedCash = sales.Where(s => !s.IsRefund && (s.PaymentMethod == "Cash" || string.IsNullOrEmpty(s.PaymentMethod)))
                                        .Sum(s => s.TotalAmount);
                
                // Calculate total refunds in cash
                var cashRefunds = sales.Where(s => s.IsRefund && (s.PaymentMethod == "Cash" || string.IsNullOrEmpty(s.PaymentMethod)))
                                       .Sum(s => s.TotalAmount);
                
                expectedCash -= cashRefunds;

                // Calculate variance (Physical - Expected)
                var variance = dialog.TotalCash - expectedCash;

                // Calculate total GST/Tax
                var totalGST = sales.Sum(s => s.Details?.Sum(d => d.TaxAmount) ?? 0);

                // Build invoice list
                var invoiceList = sales.OrderBy(s => s.InvoiceNo).Select(s => new DCRInvoiceItem
                {
                    InvoiceNo = s.InvoiceNo,
                    Time = s.Date.ToString("hh:mm tt"),
                    Amount = s.TotalAmount,
                    CashPaid = s.CashPaid,
                    OnlinePaid = s.CardPaid,
                    GST = s.Details?.Sum(d => d.TaxAmount) ?? 0,
                    ItemsCount = s.Details?.Sum(d => d.Qty) ?? 0,
                    PaymentMethod = s.PaymentMethod ?? "Cash",
                    IsRefund = s.IsRefund
                }).ToList();

                var dcr = new DCRData
                {
                    Date = DateTime.Now, // This will be formatted in XAML to hh:mm tt
                    CounterNo = CounterNo,
                    User = UserName,
                    PhysicalCash = dialog.TotalCash,
                    Notes = dialog.Notes,
                    Denominations = dialog.Items.ToList(),
                    Methods = methods,
                    TotalSales = totalSales,
                    TotalCashSales = totalCashSales,
                    TotalOnlineSales = totalOnlineSales,
                    TotalRefunds = totalRefunds,
                    TotalDiscount = totalDiscount,
                    NetSales = totalSales - totalDiscount - totalRefunds,
                    SalesCount = salesCount,
                    RefundCount = refundCount,
                    TotalItemsSold = totalItemsSold,
                    TotalRefundedItems = totalRefundedItems,
                    ExpectedCash = expectedCash,
                    Variance = variance,
                    TotalGST = totalGST,
                    InvoiceList = invoiceList
                };

                await _printService.PrintDcrReportAsync(dcr);
                StatusText = "DCR Report Printed.";
            }
            }
            catch (Exception ex)
            {
                StatusText = $"DCR Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"OpenDcr Error: {ex}");
                System.Windows.MessageBox.Show($"Failed to open DCR:\n{ex.Message}", "DCR Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task PrintByDate()
        {
            var dialog = new TextInputDialog("Reprint DCR Date (yyyy-MM-dd)", DateTime.Today.ToString("yyyy-MM-dd"));
            SafelySetDialogOwner(dialog);
            if (dialog.ShowDialog() != true) return;

            string dateStr = dialog.Result;
            string dcrPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "DCR", $"DCR_{dateStr}.png");

            if (System.IO.File.Exists(dcrPath))
            {
                StatusText = $"Reprinting DCR for {dateStr}...";
                await _printService.PrintArchiveImageAsync(dcrPath);
                StatusText = $"Historical DCR for {dateStr} printed.";
            }
            else
            {
                StatusText = $"No archived DCR found for {dateStr}.";
                System.Windows.MessageBox.Show($"Report not found for date: {dateStr}\nExpected at: {dcrPath}", "Not Found");
            }
        }

        public event Action? RequestClose;

        [RelayCommand]
        private void Close()
        {
            RequestClose?.Invoke();
        }
    }
}
