using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PosApp.Desktop.Services;

namespace PosApp.Desktop.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IScannerService _scannerService;

        [ObservableProperty]
        private string _printerName = "";

        [ObservableProperty]
        private string _shopName = "";

        [ObservableProperty]
        private string _address = "";

        [ObservableProperty]
        private string _phone1 = "";

        [ObservableProperty]
        private string _phone2 = "";
        
        [ObservableProperty]
        private string _fbrNtn = "";
        
        [ObservableProperty]
        private string _fbrStr = "";
        
        [ObservableProperty]
        private string _fbrPosId = "";

        // Scanner Settings
        [ObservableProperty]
        private string _scannerMode = "Keyboard";
        
        [ObservableProperty]
        private string _scannerComPort = "COM1";
        
        [ObservableProperty]
        private int _scannerBaudRate = 9600;

        [ObservableProperty]
        private ObservableCollection<string> _availableComPorts = new();

        [ObservableProperty]
        private ObservableCollection<string> _availableScannerModes = new() { "Keyboard", "Serial" };



        [ObservableProperty]
        private ObservableCollection<string> _installedPrinters = new();

        public SettingsViewModel(ISettingsService settingsService, IScannerService scannerService)
        {
            _settingsService = settingsService;
            _scannerService = scannerService;
            RefreshSettings();
        }

        private void LoadPrinters()
        {
            try
            {
                InstalledPrinters.Clear();
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    InstalledPrinters.Add(printer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load printers: {ex.Message}");
                InstalledPrinters.Add("Error loading printers");
            }
        }

        public void RefreshSettings()
        {
            PrinterName = _settingsService.Settings.PrinterName;
            ShopName = _settingsService.Settings.ShopName;
            Address = _settingsService.Settings.Address;
            Phone1 = _settingsService.Settings.Phone1;
            Phone2 = _settingsService.Settings.Phone2;
            FbrNtn = _settingsService.Settings.FbrNtn;
            FbrStr = _settingsService.Settings.FbrStr;
            FbrPosId = _settingsService.Settings.FbrPosId;
            
            ScannerMode = _settingsService.Settings.ScannerMode;
            ScannerComPort = _settingsService.Settings.ScannerComPort;
            ScannerBaudRate = _settingsService.Settings.ScannerBaudRate;

            LoadPrinters();
            LoadComPorts();
        }

        private void LoadComPorts()
        {
            try
            {
                AvailableComPorts.Clear();
                foreach (string port in System.IO.Ports.SerialPort.GetPortNames())
                {
                    AvailableComPorts.Add(port);
                }
                if (AvailableComPorts.Count == 0) AvailableComPorts.Add("No COM Ports found");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load COM ports: {ex.Message}");
                AvailableComPorts.Add("Error loading COM ports");
            }
        }

        [RelayCommand]
        private async Task SaveSettings()
        {
            _settingsService.Settings.PrinterName = PrinterName;
            _settingsService.Settings.ShopName = ShopName;
            _settingsService.Settings.Address = Address;
            _settingsService.Settings.Phone1 = Phone1;
            _settingsService.Settings.Phone2 = Phone2;
            _settingsService.Settings.FbrNtn = FbrNtn;
            _settingsService.Settings.FbrStr = FbrStr;
            _settingsService.Settings.FbrPosId = FbrPosId;

            _settingsService.Settings.ScannerMode = ScannerMode;
            _settingsService.Settings.ScannerComPort = ScannerComPort;
            _settingsService.Settings.ScannerBaudRate = ScannerBaudRate;

            await _settingsService.SaveSettingsAsync();
            _scannerService.Start(); // Restart scanner with new settings
            StatusText = "Settings saved successfully.";
            
            _ = Task.Run(async () => {
                await Task.Delay(3000);
                if (StatusText == "Settings saved successfully.") StatusText = "";
            });
        }

        [RelayCommand]
        private void SetupStartup()
        {
            try
            {
                string scriptPath = System.IO.Path.Combine(AppContext.BaseDirectory, "setup_startup.bat");
                if (System.IO.File.Exists(scriptPath))
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{scriptPath}\"",
                        UseShellExecute = true,
                        Verb = "runas" // Run as administrator to ensure registry access
                    };
                    System.Diagnostics.Process.Start(startInfo);
                    StatusText = "Startup setup script launched.";
                }
                else
                {
                    StatusText = "Error: setup_startup.bat not found.";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Setup Error: {ex.Message}";
            }
        }

        public event Action? RequestClose;

        [RelayCommand]
        private void Close()
        {
            RequestClose?.Invoke();
        }
    }
}
