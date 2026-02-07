using System;

namespace PosApp.Desktop.Models
{
    public class Account
    {
        public int AccountID { get; set; }
        public string? Title { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PhoneNo { get; set; }
        public string? CellPhone { get; set; }
        public string? Type { get; set; } // Consumer, Customer, Cash
        public decimal CreditLimit { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal DR { get; set; } // Debit
        public decimal CR { get; set; } // Credit
        public string? Head { get; set; }
    }
}
