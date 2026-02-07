using System;

namespace PosApp.Desktop.Models
{
    public class Packing
    {
        public int ItemCode { get; set; }
        public string? PackingType { get; set; } // Renamed from Packing to avoid conflict with class name
        public string? BarCode { get; set; }
        public decimal SPrice { get; set; }
        public decimal Qty { get; set; }
        public decimal PPrice { get; set; }
        public decimal RPrice { get; set; }
        public decimal CPrice { get; set; }
        public string? Store { get; set; }
    }
}
