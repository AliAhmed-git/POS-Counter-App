using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;

namespace PosApp.Desktop.Views
{
    public partial class DCRTemplate : UserControl
    {
        public DCRTemplate()
        {
            InitializeComponent();
            DataContext = new DCRData();
        }
    }

    public class ZeroToDashConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d && d == 0) return "-";
            if (value is int i && i == 0) return "-";
            if (value == null) return "-";
            
            if (parameter is string format && !string.IsNullOrEmpty(format))
            {
                return string.Format(culture, $"{{0:{format}}}", value);
            }
            return value?.ToString() ?? "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DCRData
    {
        public DateTime Date { get; set; }
        public int CounterNo { get; set; }
        public string? User { get; set; }
        public decimal PhysicalCash { get; set; }
        public string? Notes { get; set; }
        public List<DenominationItem> Denominations { get; set; } = new();
        public List<DenominationItem> CashDenominations { get; set; } = new();
        public List<DenominationItem> OnlineDenominations { get; set; } = new();
        public List<DCRMethodSummary> Methods { get; set; } = new();

        public decimal TotalSales { get; set; }
        public decimal TotalCashSales { get; set; }
        public decimal TotalOnlineSales { get; set; }
        public decimal TotalRefunds { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetSales { get; set; }
        public int SalesCount { get; set; }
        public int RefundCount { get; set; }
        public decimal TotalItemsSold { get; set; }
        public decimal TotalRefundedItems { get; set; }

        // Enhanced DCR Properties
        public decimal ExpectedCash { get; set; }
        public decimal Variance { get; set; } // PhysicalCash - ExpectedCash
        public string VarianceStatus 
        {
            get 
            {
                if (Math.Abs(Variance) < 0.1m) return "BALANCED";
                return Variance < 0 ? "SHORT" : "OVER";
            }
        }
        public decimal TotalGST { get; set; }
        public List<DCRInvoiceItem> InvoiceList { get; set; } = new();
    }

    public class DCRMethodSummary
    {
        public string? Method { get; set; }
        public decimal Total { get; set; }
        public decimal Fee { get; set; }
    }

    public class DCRInvoiceItem
    {
        public int InvoiceNo { get; set; }
        public string? Time { get; set; }
        public decimal Amount { get; set; }
        public decimal CashPaid { get; set; }
        public decimal OnlinePaid { get; set; }
        public decimal GST { get; set; }
        public decimal ItemsCount { get; set; }
        public string? PaymentMethod { get; set; }
        public bool IsRefund { get; set; }
    }
}
