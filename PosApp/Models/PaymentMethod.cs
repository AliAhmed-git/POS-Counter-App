using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PosApp.Desktop.Models
{
    public partial class PaymentMethod : ObservableObject
    {
        [Key]
        public string Method { get; set; } = string.Empty;
        public decimal ChargePercentage { get; set; }
        public decimal TaxPercentage { get; set; }
        public string ImagePath { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }
}
