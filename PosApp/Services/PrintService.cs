using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using PosApp.Desktop.Models;
using PosApp.Desktop.Views;

namespace PosApp.Desktop.Services
{
    public interface IPrintService
    {
        Task PrintReceiptAsync(SalesHead sale);
        Task PrintRefundReceiptAsync(SalesHead sale);
        Task PrintBarcodeTestSheetAsync(List<Packing> packings);
        Task PrintDcrReportAsync(DCRData dcr);
        Task PrintArchiveImageAsync(string filePath);
        Task DeleteArchiveEntryAsync(int invoiceNo);
    }

    public class PrintService : IPrintService
    {
        private readonly ISettingsService _settingsService;

        public PrintService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task PrintReceiptAsync(SalesHead sale)
        {
            // Generate standard Receipt ESC/POS bytecode
            byte[] bytes = GenerateReceiptEscPos(sale, false);
            await PrintToPrinterAsync(bytes);
        }

        public async Task PrintRefundReceiptAsync(SalesHead sale)
        {
            // Generate Refund Receipt ESC/POS bytecode
            byte[] bytes = GenerateReceiptEscPos(sale, true);
            await PrintToPrinterAsync(bytes);
        }

        public Task PrintBarcodeTestSheetAsync(List<Packing> packings)
        {
            // Not implemented in legacy ESC/POS service, use ModernPrintService
            return Task.CompletedTask;
        }

        public Task PrintDcrReportAsync(DCRData dcr)
        {
            // Not implemented in legacy ESC/POS service, use ModernPrintService
            return Task.CompletedTask;
        }

        public Task PrintArchiveImageAsync(string filePath)
        {
            // Not implemented in legacy ESC/POS service, use ModernPrintService
            return Task.CompletedTask;
        }

        public Task DeleteArchiveEntryAsync(int invoiceNo)
        {
            // Not implemented in legacy ESC/POS service, use ModernPrintService
            return Task.CompletedTask;
        }

        private async Task PrintToPrinterAsync(byte[] bytes)
        {
            string printerName = _settingsService.Settings.PrinterName;
            if (!string.IsNullOrWhiteSpace(printerName) && printerName != "PDF") 
            {
                await Task.Run(() => 
                {
                    try
                    {
                        // Pin the byte array to get a pointer for the P/Invoke call
                        GCHandle pinnedArray = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                        try
                        {
                            IntPtr pBytes = pinnedArray.AddrOfPinnedObject();
                            bool success = RawPrinterHelper.SendBytesToPrinter(printerName, pBytes, bytes.Length);
                            if (!success)
                            {
                                System.Diagnostics.Debug.WriteLine($"Raw printing to {printerName} failed.");
                            }
                        }
                        finally
                        {
                            pinnedArray.Free();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Printing failed: {ex.Message}");
                    }
                });
            }
        }

        private byte[] GenerateReceiptEscPos(SalesHead sale, bool isRefund)
        {
            using (var ms = new MemoryStream())
            {
                var writer = new BinaryWriter(ms);

                // ESC/POS Commands
                byte[] Initialize = { 0x1B, 0x40 };
                byte[] AlignCenter = { 0x1B, 0x61, 0x01 };
                byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
                byte[] AlignRight = { 0x1B, 0x61, 0x02 };
                byte[] BoldOn = { 0x1B, 0x45, 0x01 };
                byte[] BoldOff = { 0x1B, 0x45, 0x00 };
                byte[] DoubleWidthHeightOn = { 0x1B, 0x21, 0x30 };
                byte[] NormalSize = { 0x1B, 0x21, 0x00 };
                byte[] PaperCut = { 0x1D, 0x56, 0x41, 0x03 };

                writer.Write(Initialize);

                // --- HEADER ---
                writer.Write(AlignCenter);
                writer.Write(BoldOn);
                writer.Write(DoubleWidthHeightOn);
                writer.Write(Encoding.ASCII.GetBytes((_settingsService.Settings.ShopName ?? "ASIF SUPER STORE").ToUpper() + "\n"));
                writer.Write(NormalSize);
                
                if (isRefund)
                {
                    writer.Write(BoldOn);
                    writer.Write(DoubleWidthHeightOn);
                    writer.Write(Encoding.ASCII.GetBytes("\n*** REFUND RECEIPT ***\n"));
                    writer.Write(NormalSize);
                    writer.Write(BoldOff);
                }
                
                writer.Write(BoldOff);
                
                writer.Write(BoldOff);
                
                // Print Address
                if (!string.IsNullOrEmpty(_settingsService.Settings.Address))
                {
                    writer.Write(Encoding.ASCII.GetBytes(_settingsService.Settings.Address + "\n"));
                }
                
                // Print Phones
                if (!string.IsNullOrEmpty(_settingsService.Settings.Phone1) || !string.IsNullOrEmpty(_settingsService.Settings.Phone2))
                {
                    string phones = _settingsService.Settings.Phone1;
                    if (!string.IsNullOrEmpty(_settingsService.Settings.Phone2))
                    {
                        phones += " / " + _settingsService.Settings.Phone2;
                    }
                    writer.Write(Encoding.ASCII.GetBytes(phones + "\n"));
                }

                writer.Write(Encoding.ASCII.GetBytes("STATIONARY | GROCERY | COSMETICS\n"));
                writer.Write(Encoding.ASCII.GetBytes("STRN: 1234567890123  NTN: 1234567-8\n"));
                writer.Write(Encoding.ASCII.GetBytes("==========================================\n"));

                // --- METADATA ---
                writer.Write(AlignLeft);
                writer.Write(Encoding.ASCII.GetBytes($"DATE: {DateTime.Now:dd/MM/yy} TIME: {DateTime.Now:hh:mm tt}\n"));
                writer.Write(Encoding.ASCII.GetBytes($"COUNTER: {sale.CounterNo}\n"));
                writer.Write(Encoding.ASCII.GetBytes($"BILL: {sale.InvoiceNo.ToString().PadRight(23)}"));
                writer.Write(Encoding.ASCII.GetBytes($"ACC:  {(string.IsNullOrEmpty(sale.CustomerName) || sale.CustomerName == "Walking Customer" ? "WALKING CUSTOMER" : sale.CustomerName.ToUpper())}\n"));
                writer.Write(Encoding.ASCII.GetBytes("------------------------------------------\n"));

                // --- TABLE HEADER ---
                // Columns: Item (18), Qty(4), Rate(9), Total(9) -> Total 40 (standard width)
                writer.Write(BoldOn);
                writer.Write(Encoding.ASCII.GetBytes("ITEM DESCRIPTION".PadRight(18)));
                writer.Write(Encoding.ASCII.GetBytes("QTY".PadLeft(4)));
                writer.Write(Encoding.ASCII.GetBytes("RATE".PadLeft(9)));
                writer.Write(Encoding.ASCII.GetBytes("TOTAL".PadLeft(9) + "\n"));
                writer.Write(BoldOff);
                writer.Write(Encoding.ASCII.GetBytes("------------------------------------------\n"));

                // --- ITEMS ---
                int index = 1;
                foreach (var item in sale.Details)
                {
                    string name = item.ItemName ?? "Unknown";
                    string detailPrefix = $"{index}. ";
                    
                    // Print Item Name in Bold or regular, but on its own line if long
                    string displayName = (detailPrefix + name.ToUpper());
                    if (displayName.Length > 40) displayName = displayName.Substring(0, 37) + "...";
                    writer.Write(Encoding.ASCII.GetBytes(displayName + "\n"));
                    
                    // Values line
                    string qtyStr = item.Qty.ToString("0.###").PadLeft(22); // Align under description/qty area
                    string rateStr = item.SPrice.ToString("N0").PadLeft(9);
                    string totalStr = item.NetAmount.ToString("N0").PadLeft(9);

                    writer.Write(Encoding.ASCII.GetBytes($"{qtyStr}{rateStr}{totalStr}\n"));
                    index++;
                }
                writer.Write(Encoding.ASCII.GetBytes("------------------------------------------\n"));

                // --- TOTALS ---
                writer.Write(AlignRight);
                decimal totalGst = sale.Details.Sum(d => d.TaxAmount);
                decimal grossTotal = sale.SubTotal;

                if (isRefund)
                {
                     writer.Write(Encoding.ASCII.GetBytes($"REFUND TOTAL:  {sale.TotalAmount,15:N0}\n"));
                }
                else
                {
                    writer.Write(Encoding.ASCII.GetBytes($"GROSS TOTAL: {grossTotal,15:N0}\n"));
                    
                    if (totalGst > 0)
                    {
                        writer.Write(Encoding.ASCII.GetBytes($"FBR TAX (GST): {totalGst,14:N0}\n"));
                    }
                    
                    if (sale.InvoiceDiscount > 0)
                    {
                        writer.Write(Encoding.ASCII.GetBytes($"DISCOUNT:      {sale.InvoiceDiscount,15:N0}\n"));
                    }

                    if (sale.ServiceCharge > 0)
                    {
                        writer.Write(Encoding.ASCII.GetBytes($"ONLINE CHARGES:{sale.ServiceCharge,15:N0}\n"));
                    }

                    writer.Write(BoldOn);
                    writer.Write(Encoding.ASCII.GetBytes($"NET PAYABLE:   {sale.TotalAmount,15:N0}\n"));
                    writer.Write(BoldOff);
                }
                writer.Write(Encoding.ASCII.GetBytes("==========================================\n"));

                if (!isRefund)
                {
                    writer.Write(Encoding.ASCII.GetBytes($"CASH RECEIVED: {sale.CashPaid,15:N0}\n"));
                    
                    if (sale.CardPaid > 0)
                    {
                        string methodLabel = (sale.PaymentMethod?.ToUpper() ?? "ONLINE") + " PAID:";
                        writer.Write(Encoding.ASCII.GetBytes($"{methodLabel.PadRight(15)} {sale.CardPaid,15:N0}\n"));
                        if (sale.ServiceCharge > 0)
                        {
                            writer.Write(Encoding.ASCII.GetBytes($" (Inc. Fees {sale.ServiceCharge:N0})\n"));
                        }
                    }

                    decimal totalReceived = sale.CashPaid + sale.CardPaid;
                    decimal balance = totalReceived - sale.TotalAmount;
                    writer.Write(Encoding.ASCII.GetBytes($"BALANCE DUE:   {(balance > 0 ? balance : 0),15:N0}\n"));
                    writer.Write(Encoding.ASCII.GetBytes("------------------------------------------\n"));
                }

                // --- FOOTER & POLICY ---
                writer.Write(AlignCenter);
                writer.Write(BoldOn);
                if (isRefund)
                {
                    writer.Write(Encoding.ASCII.GetBytes("\n*** REFUND PROCESSED ***\n"));
                }
                else
                {
                    writer.Write(Encoding.ASCII.GetBytes("\n*** THANKS FOR SHOPPING ***\n"));
                }
                writer.Write(BoldOff);
                
                writer.Write(Encoding.ASCII.GetBytes("NOTE: For return or exchange please bring\n"));
                writer.Write(Encoding.ASCII.GetBytes("original invoice. Goods purchased can be\n"));
                writer.Write(Encoding.ASCII.GetBytes("conditionally changed within 2 days.\n"));
                writer.Write(Encoding.ASCII.GetBytes("Imported items will not be changed.\n\n"));
                
                writer.Write(BoldOn);
                writer.Write(Encoding.ASCII.GetBytes("SHOP TIMINGS: 11:00 AM TO 11:00 PM\n"));
                writer.Write(BoldOff);
                
                writer.Write(Encoding.ASCII.GetBytes("\nSoftware by: SWIFT DEVELOPER\n\n\n"));
                
                writer.Write(PaperCut);

                return ms.ToArray();
            }
        }
    }
}
