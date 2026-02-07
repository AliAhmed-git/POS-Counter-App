using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Text.Json;
using PosApp.Desktop.Models;
using PosApp.Desktop.Views;

namespace PosApp.Desktop.Services
{
    public class ModernPrintService : IPrintService
    {
        private readonly ISettingsService _settingsService;

        public ModernPrintService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task PrintReceiptAsync(SalesHead sale)
        {
            await PrintVisualReceiptAsync(sale, false);
        }

        public async Task PrintRefundReceiptAsync(SalesHead sale)
        {
            await PrintVisualReceiptAsync(sale, true);
        }

        private async Task PrintVisualReceiptAsync(SalesHead sale, bool isRefund)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var template = new ReceiptTemplate
                    {
                        DataContext = sale,
                        ShopName = _settingsService.Settings.ShopName,
                        Address = _settingsService.Settings.Address,
                        Phone = !string.IsNullOrEmpty(_settingsService.Settings.Phone2) 
                                ? $"{_settingsService.Settings.Phone1} / {_settingsService.Settings.Phone2}"
                                : _settingsService.Settings.Phone1,
                        Time = DateTime.Now.ToString("t"),
                        FbrNtn = _settingsService.Settings.FbrNtn,
                        FbrStr = _settingsService.Settings.FbrStr,
                        FbrPosId = _settingsService.Settings.FbrPosId,
                        FbrInvoiceNo = $"{_settingsService.Settings.FbrPosId}-{sale.InvoiceNo}",
                        IsRefund = isRefund
                    };

                    double width = 270;
                    template.Measure(new Size(width, double.PositiveInfinity));
                    template.Arrange(new Rect(0, 0, width, template.DesiredSize.Height));
                    
                    // Set quality options on the template itself
                    TextOptions.SetTextFormattingMode(template, TextFormattingMode.Display);
                    TextOptions.SetTextRenderingMode(template, TextRenderingMode.ClearType);
                    RenderOptions.SetBitmapScalingMode(template, BitmapScalingMode.HighQuality);
                    
                    template.UpdateLayout();
                    
                    // Force a faster pass
                    await Task.Yield(); 
                    template.UpdateLayout();

                    // ARCHIVE RECEIPT
                    string reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                    string receiptFolder = Path.Combine(reportsFolder, "Receipts");
                    Directory.CreateDirectory(receiptFolder);
                    string fileName = isRefund ? $"Refund_{sale.InvoiceNo}.png" : $"Receipt_{sale.InvoiceNo}.png";
                    await SaveVisualToArchiveAsync(template, Path.Combine(receiptFolder, fileName));

                    // JSON HISTORY
                    _ = ArchiveSaleToJsonAsync(sale, Path.Combine(reportsFolder, "receipt_history.json"));

                    PrintDialog printDialog = new PrintDialog();
                    string printerName = _settingsService.Settings.PrinterName;
                    if (!string.IsNullOrWhiteSpace(printerName) && printerName != "PDF")
                    {
                        try 
                        {
                            var queue = new System.Printing.LocalPrintServer().GetPrintQueue(printerName);
                            if (queue != null) printDialog.PrintQueue = queue;
                        }
                        catch { }
                    }

                    string jobName = isRefund ? $"Refund_{sale.InvoiceNo}" : $"Invoice_{sale.InvoiceNo}";
                    printDialog.PrintVisual(template, jobName);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Modern Printing failed: {ex.Message}");
                }
            });
        }
        public async Task PrintBarcodeTestSheetAsync(List<Packing> packings)
        {
            if (packings == null || !packings.Any()) return;

            await App.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var stack = new StackPanel { Width = 300, Background = Brushes.White };
                    stack.Children.Add(new TextBlock { Text = "BARCODE TEST SHEET", FontSize = 16, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 15) });

                    foreach (var p in packings)
                    {
                        stack.Children.Add(new TextBlock { Text = $"{p.ItemCode} - {p.PackingType}", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) });
                        
                        var barcodeVisual = CreateBarcodeVisual(p.BarCode ?? p.ItemCode.ToString());
                        barcodeVisual.HorizontalAlignment = HorizontalAlignment.Center;
                        barcodeVisual.Margin = new Thickness(0, 5, 0, 5);
                        stack.Children.Add(barcodeVisual);

                        stack.Children.Add(new TextBlock { Text = p.BarCode ?? p.ItemCode.ToString(), FontSize = 12, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) });

                        // Extra Large separator line
                        stack.Children.Add(new System.Windows.Shapes.Rectangle { Height = 1, Fill = Brushes.Black, Margin = new Thickness(0, 5, 0, 15), HorizontalAlignment = HorizontalAlignment.Stretch });
                    }

                    stack.Children.Add(new TextBlock { Text = $"Printed: {DateTime.Now:g}", FontSize = 10, Margin = new Thickness(0, 10, 0, 0), Opacity = 0.6 });

                    await PrintVisualAsync(stack, "Barcode Test");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Barcode printing failed: {ex.Message}", "Printing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private FrameworkElement CreateBarcodeVisual(string code)
        {
            // Extended height for "Extra Large" requirement
            const double barcodeHeight = 80;
            var canvas = new Canvas { Height = barcodeHeight, Width = 300, Margin = new Thickness(5) };
            double x = 0;
            
            var patterns = new Dictionary<char, string>
            {
                {'0', "nnnwwnwnn"}, {'1', "wnnwnnnnw"}, {'2', "nnwwnnnnw"}, {'3', "wnwwnnnnn"},
                {'4', "nnnwwnnnw"}, {'5', "wnnwwnnnn"}, {'6', "nnwwnwnnn"}, {'7', "nnnnwnwnw"},
                {'8', "wnnnwnwnn"}, {'9', "nnnwwnwnn"}, {'*', "nwnnwnwnn"} 
            };

            string fullCode = "*" + code + "*";
            foreach (char c in fullCode)
            {
                if (!patterns.ContainsKey(c)) continue;
                string pattern = patterns[c];
                
                foreach (char bit in pattern)
                {
                    // Scaled widths for high visibility
                    double w = bit == 'w' ? 4 : 1.5;
                    var rect = new System.Windows.Shapes.Rectangle 
                    { 
                        Width = w, 
                        Height = barcodeHeight, 
                        Fill = Brushes.Black 
                    };
                    Canvas.SetLeft(rect, x);
                    canvas.Children.Add(rect);
                    x += w + 1; 
                }
                x += 3; 
            }

            canvas.Width = x;
            return canvas;
        }

        private Task PrintVisualAsync(Visual visual, string jobName)
        {
            try
            {
                var settings = _settingsService.Settings;
                string printerName = settings.PrinterName ?? "";

                PrintDialog printDialog = new PrintDialog();
                if (!string.IsNullOrEmpty(printerName))
                {
                    printDialog.PrintQueue = new System.Printing.PrintServer().GetPrintQueue(printerName);
                }

                printDialog.PrintVisual(visual, jobName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Printing failed: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        public async Task PrintDcrReportAsync(DCRData dcr)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var template = new DCRTemplate
                    {
                        DataContext = dcr
                    };

                    double width = 270;
                    template.Measure(new Size(width, double.PositiveInfinity));
                    template.Arrange(new Rect(0, 0, width, template.DesiredSize.Height));
                    
                    TextOptions.SetTextFormattingMode(template, TextFormattingMode.Display);
                    TextOptions.SetTextRenderingMode(template, TextRenderingMode.ClearType);
                    
                    template.UpdateLayout();

                    // ARCHIVE DCR
                    string dcrFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "DCR");
                    Directory.CreateDirectory(dcrFolder);
                    string fileName = $"DCR_{dcr.Date:yyyy-MM-dd}.png";
                    await SaveVisualToArchiveAsync(template, Path.Combine(dcrFolder, fileName));

                    PrintDialog printDialog = new PrintDialog();
                    string printerName = _settingsService.Settings.PrinterName;
                    if (!string.IsNullOrWhiteSpace(printerName) && printerName != "PDF")
                    {
                        try
                        {
                            var queue = new System.Printing.LocalPrintServer().GetPrintQueue(printerName);
                            if (queue != null) printDialog.PrintQueue = queue;
                        }
                        catch { }
                    }

                    printDialog.PrintVisual(template, $"DCR_{dcr.Date:yyyyMMdd}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"DCR printing failed: {ex.Message}");
                }
            });
        }

        private async Task SaveVisualToArchiveAsync(FrameworkElement visual, string filePath)
        {
            try
            {
                // HIGH DPI: Use 600 DPI for ultra-sharp archiving and reprinting
                double dpi = 600; 
                double scale = dpi / 96.0;

                double width = visual.ActualWidth;
                double height = visual.ActualHeight;
                if (width <= 0) width = 270;
                if (height <= 0) height = visual.DesiredSize.Height > 0 ? visual.DesiredSize.Height : 1000;

                int pixelWidth = (int)Math.Max(1, width * scale);
                int pixelHeight = (int)Math.Max(1, height * scale);

                var renderTarget = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
                
                // Clear background to white for printing compatibility
                var drawingVisual = new DrawingVisual();
                using (var ctx = drawingVisual.RenderOpen())
                {
                    ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                    ctx.DrawRectangle(new VisualBrush(visual), null, new Rect(0, 0, width, height));
                }
                
                renderTarget.Render(drawingVisual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                using (var stream = File.Create(filePath))
                {
                    encoder.Save(stream);
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Archiving failed: {ex.Message}");
            }
        }

        public async Task PrintArchiveImageAsync(string filePath)
        {
            if (!File.Exists(filePath)) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var bitmap = new BitmapImage();
                    using (var stream = File.OpenRead(filePath))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                    }

                    var image = new Image 
                    { 
                        Source = bitmap, 
                        Width = 270,
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    
                    // CRISP REPRINTING: Use NearestNeighbor or HighQuality with specific hints
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                    RenderOptions.SetEdgeMode(image, EdgeMode.Aliased); // Sharp edges for text-heavy images
                    
                    image.Measure(new Size(270, double.PositiveInfinity));
                    image.Arrange(new Rect(0, 0, 270, image.DesiredSize.Height));

                    PrintDialog printDialog = new PrintDialog();
                    string printerName = _settingsService.Settings.PrinterName;
                    if (!string.IsNullOrWhiteSpace(printerName) && printerName != "PDF")
                    {
                        try
                        {
                            var queue = new System.Printing.LocalPrintServer().GetPrintQueue(printerName);
                            if (queue != null) printDialog.PrintQueue = queue;
                        }
                        catch { }
                    }

                    printDialog.PrintVisual(image, "Reprint Archived");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Reprint failed: {ex.Message}");
                }
            });
        }

        private async Task ArchiveSaleToJsonAsync(SalesHead sale, string filePath)
        {
            try
            {
                List<SalesHead> history = new();
                if (File.Exists(filePath))
                {
                    string existingJson = await File.ReadAllTextAsync(filePath);
                    history = JsonSerializer.Deserialize<List<SalesHead>>(existingJson) ?? new();
                }

                history.Add(sale);
                string json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON Archiving failed: {ex.Message}");
            }
        }

        public async Task DeleteArchiveEntryAsync(int invoiceNo)
        {
            try
            {
                // 1. Remove from JSON history
                string reportsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                string jsonPath = Path.Combine(reportsFolder, "receipt_history.json");
                
                if (File.Exists(jsonPath))
                {
                    string existingJson = await File.ReadAllTextAsync(jsonPath);
                    var history = JsonSerializer.Deserialize<List<SalesHead>>(existingJson) ?? new();
                    int initialCount = history.Count;
                    history.RemoveAll(s => s.InvoiceNo == invoiceNo && !s.IsRefund);
                    
                    if (history.Count != initialCount)
                    {
                        string json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(jsonPath, json);
                    }
                }

                // 2. Delete PNG Archive
                string receiptFolder = Path.Combine(reportsFolder, "Receipts");
                string pngPath = Path.Combine(receiptFolder, $"Receipt_{invoiceNo}.png");
                if (File.Exists(pngPath))
                {
                    File.Delete(pngPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"History cleanup failed for invoice {invoiceNo}: {ex.Message}");
            }
        }
    }
}
