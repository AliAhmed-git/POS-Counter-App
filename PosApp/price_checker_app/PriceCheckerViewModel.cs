using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PosApp.Desktop.Services;
using PosApp.Desktop.Models;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PriceChecker
{
    public partial class PriceCheckerViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IScannerService _scannerService;
        private readonly Dispatcher _dispatcher;

        [ObservableProperty]
        private string _itemName = "PLEASE SCAN BARCODE";

        [ObservableProperty]
        private string _packing = "";

        [ObservableProperty]
        private decimal _retailPrice;

        [ObservableProperty]
        private decimal _discountPrice;

        [ObservableProperty]
        private bool _isDataVisible = false;

        [ObservableProperty]
        private bool _isRetailVisible = false;

        [ObservableProperty]
        private string _statusMessage = "Ready to scan";

        [ObservableProperty]
        private decimal _discountAmount;

        [ObservableProperty]
        private string _apiStatus = "API: INITIALIZING";

        [ObservableProperty]
        private string _testBarcode = "";

        [RelayCommand]
        private async Task RunTestBarcode()
        {
            if (string.IsNullOrWhiteSpace(TestBarcode)) return;
            await LookupItem(TestBarcode);
            TestBarcode = "";
        }

        private readonly IAppStatusService _statusService;
        private DispatcherTimer _resetTimer;

        public PriceCheckerViewModel(IDataService dataService, IScannerService scannerService, IAppStatusService statusService)
        {
            _dataService = dataService;
            _scannerService = scannerService;
            _statusService = statusService;
            _dispatcher = Dispatcher.CurrentDispatcher;

            ApiStatus = _statusService.ApiStatus;
            _statusService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IAppStatusService.ApiStatus))
                {
                    ApiStatus = _statusService.ApiStatus;
                }
            };

            _scannerService.BarcodeScanned += OnBarcodeScanned;
            _scannerService.Start();

            _resetTimer = new DispatcherTimer();
            _resetTimer.Interval = TimeSpan.FromSeconds(10);
            _resetTimer.Tick += (s, e) => ResetUI();
        }

        private void OnBarcodeScanned(string barcode)
        {
            _dispatcher.Invoke(async () => await LookupItem(barcode));
        }

        private async Task LookupItem(string barcode)
        {
            try
            {
                StatusMessage = "Looking up...";
                var item = await _dataService.GetItemByBarcodeAsync(barcode);
                if (item != null)
                {
                    ItemName = item.ItemName ?? "Unknown Item";
                    Packing = item.Packing ?? "Standard";
                    RetailPrice = item.RPrice;
                    DiscountPrice = item.PPrice;
                    
                    if (RetailPrice > DiscountPrice)
                    {
                        IsRetailVisible = true;
                        DiscountAmount = RetailPrice - DiscountPrice;
                    }
                    else
                    {
                        IsRetailVisible = false;
                        DiscountAmount = 0;
                    }

                    IsDataVisible = true;
                    StatusMessage = "Item Found";
                    
                    _resetTimer.Stop();
                    _resetTimer.Start();
                }
                else
                {
                    StatusMessage = "Item Not Found";
                    IsDataVisible = false;
                    ItemName = "ITEM NOT FOUND";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        private void ResetUI()
        {
            IsDataVisible = false;
            IsRetailVisible = false;
            ItemName = "PLEASE SCAN BARCODE";
            Packing = "";
            RetailPrice = 0;
            DiscountPrice = 0;
            StatusMessage = "Ready to scan";
            _resetTimer.Stop();
        }
    }
}
