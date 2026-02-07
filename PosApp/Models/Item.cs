using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace PosApp.Desktop.Models
{
    public class Item
    {
        public int ItemCode { get; set; }
        public string? ItemName { get; set; }
        public string? Company { get; set; }
        public string? Packing { get; set; }
        public int PackQty { get; set; }
        public decimal PPrice { get; set; } // Purchase Price
        public decimal SPrice { get; set; } // Sale Price
        public decimal RPrice { get; set; } // Retail Price
        public decimal CPrice { get; set; } // Cost Price
        public decimal Stock { get; set; }
        public decimal STax { get; set; }
        public string? UrduDesc { get; set; }
        public string? Type { get; set; }
        public string? Group { get; set; }
        public string? Location { get; set; }
        public DateTime? StockDate { get; set; }
        [NotMapped]
        public bool HasMultiplePackings { get; set; }
    }
}
