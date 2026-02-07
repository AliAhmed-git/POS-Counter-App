using System;

namespace PosApp.Desktop.Models
{
    public class Login
    {
        public string User { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string? Roll { get; set; } // Admin, DEO, etc.
        public int SalesmanID { get; set; }
    }

    public class CounterInfo
    {
        public int CounterNo { get; set; }
        public string? CounterName { get; set; }
        public int StartInvoice { get; set; }
        public int EndInvoice { get; set; }
        public string? SupervisorKey { get; set; }
    }
}
