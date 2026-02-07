using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PosApp.Desktop.Services
{
    public class AppSettings
    {
        public string PrinterName { get; set; } = "Microsoft Print to PDF";
        public string ShopName { get; set; } = "ASIF SUPER STORE";
        public string Address { get; set; } = "Shop #1, Main Market";
        public string Phone1 { get; set; } = "0300-1234567";
        public string Phone2 { get; set; } = "";
        public string OutputFilePath { get; set; } = "";
        public int CounterNo { get; set; } = 1;
        public string FbrNtn { get; set; } = "";
        public string FbrStr { get; set; } = "";
        public string FbrPosId { get; set; } = "";

        // Scanner Settings
        public string ScannerMode { get; set; } = "Keyboard"; // Keyboard or Serial
        public string ScannerComPort { get; set; } = "COM1";
        public int ScannerBaudRate { get; set; } = 9600;
    }

    public interface ISettingsService
    {
        AppSettings Settings { get; }
        Task LoadSettingsAsync();
        Task SaveSettingsAsync();
    }

    public class SettingsService : ISettingsService
    {
        private readonly string SettingsFileName = Path.Combine(AppContext.BaseDirectory, "local.settings.json");
        public AppSettings Settings { get; private set; } = new AppSettings();

        public async Task LoadSettingsAsync()
        {
            if (File.Exists(SettingsFileName))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(SettingsFileName);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null) Settings = loaded;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                }
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(SettingsFileName, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
