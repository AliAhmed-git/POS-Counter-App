using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace PosApp.Desktop.Models
{
    public partial class SalesHead
    {
        public int InvoiceNo { get; set; }
        public DateTime Date { get; set; }
        public int AccountID { get; set; }
        public string? CustomerName { get; set; }
        public string? SalesmanID { get; set; }
        public decimal CashPaid { get; set; }
        public decimal PreviousBal { get; set; }
        public string? Posting { get; set; } // E, Y, S, U
        public string? User { get; set; }
        public int CounterNo { get; set; }
        public string? InvoiceType { get; set; }
        public decimal InvoiceDiscount { get; set; }
        public decimal TotalAmount { get; set; }
        
        // Added back for Card/Wallet payments and Refund state
        public string? PaymentMethod { get; set; }
        public decimal CardPaid { get; set; }
        public decimal ServiceCharge { get; set; }
        public bool IsRefund { get; set; }
        
        public virtual ICollection<SalesDetail> Details { get; set; } = new List<SalesDetail>();

        // Calculated properties for receipt
        public decimal GrossTotal => Details.Sum(d => d.SPrice * d.Qty);
        public decimal TotalLineDiscount => Details.Sum(d => d.Discount);
        public decimal EffectiveTotalDiscount => TotalLineDiscount + InvoiceDiscount;
        public decimal TotalTax => Details.Sum(d => d.TaxAmount);
        public decimal TotalItems => Details.Sum(d => d.Qty);
        public decimal SubTotal => GrossTotal; // Amount before discount
        public decimal ChangeDue => Math.Max(0, CashPaid + CardPaid - TotalAmount);
    }

    public partial class SalesDetail : ObservableObject
    {
        public int InvoiceNo { get; set; }
        public int ItemCode { get; set; }
        public int LineNo { get; set; }
        public string? ItemName { get; set; }
        public string? Company { get; set; }
        [ObservableProperty]
        private string? _packing;

        [ObservableProperty]
        private decimal _qty;

        [ObservableProperty]
        private decimal _sPrice;

        [ObservableProperty]
        private decimal _pPrice; // Purchase Price at time of sale

        [ObservableProperty]
        private decimal _rPrice; // Retail Price

        [ObservableProperty]
        private decimal _discount;

        [ObservableProperty]
        private decimal _taxAmount;

        [ObservableProperty]
        private decimal _netAmount;

        [ObservableProperty]
        [property: NotMapped]
        private bool _isFlash;

        [ObservableProperty]
        [property: NotMapped]
        private bool _hasMultiplePackings;

        [NotMapped]
        public decimal OriginalSPrice { get; set; }



        public string? BatchNo { get; set; }

        [NotMapped]
        public ObservableCollection<Packing> AvailablePackings { get; } = new();

        [NotMapped]
        private Packing? _selectedPacking;
        [NotMapped]
        public Packing? SelectedPacking
        {
            get => _selectedPacking;
            set
            {
                if (SetProperty(ref _selectedPacking, value) && value != null)
                {
                    Packing = value.PackingType;
                    SPrice = value.SPrice;
                    OriginalSPrice = value.SPrice;
                    PPrice = value.PPrice;
                    RPrice = value.RPrice;
                    // Triggering PropertyChanged manually for fields that might not be auto-notifying if needed
                    // but SPrice is an ObservableProperty, so it should be fine.
                }
            }
        }

        [NotMapped]
        public bool IsWeightUnit
        {
            get
            {
                if (string.IsNullOrEmpty(Packing)) return false;
                string p = Packing.ToLower().Trim();
                
                // Pure weight units
                if (p == "g" || p == "kg" || p == "grams" || p == "gram") return true;
                
                // Packings ending with weight units (e.g. "1kg", "500g", "2.5 kg")
                if (p.EndsWith("kg") || p.EndsWith("g") || p.EndsWith("grams") || p.EndsWith("gram"))
                {
                    // Check if there's a number before the unit
                    string unit = "";
                    if (p.EndsWith("grams")) unit = "grams";
                    else if (p.EndsWith("gram")) unit = "gram";
                    else if (p.EndsWith("kg")) unit = "kg";
                    else if (p.EndsWith("g")) unit = "g";

                    string prefix = p.Substring(0, p.Length - unit.Length).Trim();
                    // If it's a number followed by unit, it's a weight-based packing
                    if (decimal.TryParse(prefix, out _)) return true;
                }
                
                return false;
            }
        }
    }
}
