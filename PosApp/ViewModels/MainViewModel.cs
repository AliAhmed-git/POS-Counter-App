using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using PosApp.Desktop.Models;
using PosApp.Desktop.Services;
using PosApp.Desktop.Views;

namespace PosApp.Desktop.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly ISyncService _syncService;
        private readonly ISettingsService _settingsService;
        private readonly LoginViewModel _loginViewModel;
        private readonly SaleViewModel _saleViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly DCRViewModel _dcrViewModel;
        private DateTime _lastSyncTime = DateTime.MinValue;
        private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1);

        [ObservableProperty]
        private string _userName = "User";

        [ObservableProperty]
        private string _counterName = "C0";

        [ObservableProperty]
        private ViewModelBase? _currentViewModel;

        [ObservableProperty]
        private bool _isKioskMode = true;

        [ObservableProperty]
        private WindowState _windowState = WindowState.Maximized;

        [ObservableProperty]
        private WindowStyle _windowStyle = WindowStyle.None;

        [ObservableProperty]
        private bool _isTopmost = true;

        [ObservableProperty]
        private ResizeMode _resizeMode = ResizeMode.NoResize;

        [ObservableProperty]
        private string _syncStatusText = "Net Speed: -- ms";
        
        [ObservableProperty]
        private System.Windows.Media.Brush _syncStatusColor = System.Windows.Media.Brushes.LimeGreen;

        [ObservableProperty]
        private string _pingMs = "-- ms";

        [ObservableProperty]
        private string _nextSyncIn = "--:--";

        [ObservableProperty]
        private string _statusText = "Ready";
        public MainViewModel(IDataService dataService, ISyncService syncService, ISettingsService settingsService, LoginViewModel loginViewModel, SaleViewModel saleViewModel, SettingsViewModel settingsViewModel, DCRViewModel dcrViewModel)
        {
            _dataService = dataService;
            _syncService = syncService;
            _settingsService = settingsService;
            _loginViewModel = loginViewModel;
            _saleViewModel = saleViewModel;
            _settingsViewModel = settingsViewModel;
            _dcrViewModel = dcrViewModel;
            
            _loginViewModel.OnLoginSuccess += OnLoginSuccess;
            _settingsViewModel.RequestClose += NavigateToSale;
            _dcrViewModel.RequestClose += NavigateToSale;

            // Start periodic sync
            _ = StartPeriodicSyncAsync();
            
            // Sync status text from child viewmodels
            _loginViewModel.PropertyChanged += OnChildViewModelPropertyChanged;
            _saleViewModel.PropertyChanged += OnChildViewModelPropertyChanged;
            
            CurrentViewModel = _loginViewModel; // Start with Login
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            string requiredKey = "123";
            if (_saleViewModel.CurrentCounter != null && !string.IsNullOrEmpty(_saleViewModel.CurrentCounter.SupervisorKey))
            {
                requiredKey = _saleViewModel.CurrentCounter.SupervisorKey;
            }

            var dialog = new PasswordDialog(requiredKey, "SUPERVISOR PASSWORD");
            SafelySetDialogOwner(dialog);
            if (dialog.ShowDialog() != true && !dialog.IsVerified)
            {
                return;
            }

            _settingsViewModel.RefreshSettings();
            CurrentViewModel = _settingsViewModel;
            StatusText = "Settings";
        }

        [RelayCommand]
        private void NavigateToSale()
        {
            CurrentViewModel = _saleViewModel;
            StatusText = "Ready";
        }

        [RelayCommand]
        private void NavigateToDCR()
        {
            CurrentViewModel = _dcrViewModel;
            StatusText = "Daily Cash Report";
        }

        private void OnChildViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StatusText) && sender is ViewModelBase childVm && childVm == CurrentViewModel)
            {
                StatusText = childVm.StatusText;
            }
        }

        private void OnLoginSuccess(Login user, CounterInfo counter)
        {
            UserName = user.User;
            CounterName = counter.CounterName ?? $"Counter {counter.CounterNo:D2}";
            _saleViewModel.CurrentUser = user;
            _saleViewModel.CurrentCounter = counter;
            
            // Set DCR info
            _dcrViewModel.UserName = user.User;
            _dcrViewModel.CounterNo = counter.CounterNo;
            
            CurrentViewModel = _saleViewModel;
            StatusText = $"Welcome, {UserName}. Access granted at {CounterName}.";

            // Trigger sync on login
            _ = PerformSyncAsync();
        }

        [RelayCommand]
        private void Logout()
        {
            UserName = "User";
            CurrentViewModel = _loginViewModel;
            StatusText = "Logged out.";
        }

        [RelayCommand]
        private void ToggleKioskMode()
        {
            if (!IsKioskMode)
            {
                // Entering Kiosk Mode
                IsKioskMode = true;
                WindowStyle = WindowStyle.None;
                IsTopmost = true;
                ResizeMode = ResizeMode.NoResize;
                
                // Force a state refresh to ensure fullscreen covers taskbar consistently
                WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
                
                StatusText = "KIOSK MODE ACTIVE - SYSTEM RESTRICTED";
            }
            else
            {
                // Exiting Kiosk Mode - Require Supervisor Key
                string requiredKey = "123";
                if (_saleViewModel.CurrentCounter != null && !string.IsNullOrEmpty(_saleViewModel.CurrentCounter.SupervisorKey))
                {
                    requiredKey = _saleViewModel.CurrentCounter.SupervisorKey;
                }

                var dialog = new PasswordDialog(requiredKey, "SUPERVISOR PASSWORD");
                SafelySetDialogOwner(dialog);
                if (dialog.ShowDialog() != true && !dialog.IsVerified)
                {
                    return;
                }

                IsKioskMode = false;
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
                IsTopmost = false;
                ResizeMode = ResizeMode.CanResize;
                StatusText = "KIOSK MODE DEACTIVATED";
            }
        }


        [RelayCommand]
        private void SearchItems(string criteria)
        {
            StatusText = "Searching...";
            // Logic handled by service
            StatusText = "Ready";
        }

        public async Task TriggerBackupAsync()
        {
            await _dataService.BackupDatabaseAsync();
        }

        private async Task StartPeriodicSyncAsync()
        {
            while (true)
            {
                try
                {
                    DateTime now = DateTime.Now;
                    if (now - _lastSyncTime >= _syncInterval)
                    {
                        var timestamp = now.ToString("HH:mm:ss");
                        SyncStatusText = "Syncing...";
                        SyncStatusColor = System.Windows.Media.Brushes.Yellow;

                        int businessId = 41; // Corrected from Postman collection
                        int counterNo = 11; // Bumped to 11 to force full download

                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        var syncData = await _syncService.DownloadUpdatedItemsAsync(businessId, counterNo);
                        watch.Stop();
                        PingMs = $"{watch.ElapsedMilliseconds} ms";

                        if (syncData != null)
                        {
                            if (syncData.Count > 0)
                            {
                                await _dataService.SyncItemsAsync(syncData);
                                await _syncService.ConfirmSyncAsync(businessId, counterNo);
                            }
                            SyncStatusText = $"Net Speed: {PingMs}";
                            SyncStatusColor = System.Windows.Media.Brushes.LimeGreen;
                            _lastSyncTime = DateTime.Now;
                        }
                        else
                        {
                            SyncStatusText = "Offline";
                            SyncStatusColor = System.Windows.Media.Brushes.Red;
                        }
                    }

                    // Update countdown
                    var nextSync = _lastSyncTime.Add(_syncInterval);
                    var remaining = nextSync - DateTime.Now;
                    if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                    NextSyncIn = $"Next sync in: {remaining.Minutes:D2}:{remaining.Seconds:D2}";
                }
                catch (Exception)
                {
                    SyncStatusText = "Sync Error";
                    SyncStatusColor = System.Windows.Media.Brushes.Red;
                }

                await Task.Delay(1000); // Update countdown every second
            }
        }

        [RelayCommand]
        public async Task PerformSyncAsync()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                SyncStatusText = $"Manual Sync... ({timestamp})";
                SyncStatusColor = System.Windows.Media.Brushes.Yellow;

                int businessId = 40; 
                int counterNo = 11; // Bumped to 11 to force full download

                var watch = System.Diagnostics.Stopwatch.StartNew();
                var syncData = await _syncService.DownloadUpdatedItemsAsync(businessId, counterNo);
                watch.Stop();
                PingMs = $"{watch.ElapsedMilliseconds} ms";
                if (syncData != null)
                {
                    if (syncData.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{timestamp}] Syncing {syncData.Count} products to database...");
                        await _dataService.SyncItemsAsync(syncData);
                        await _syncService.ConfirmSyncAsync(businessId, counterNo);
                        
                        int totalPackings = syncData.Sum(s => s.Packings?.Count ?? 0);
                        SyncStatusText = $"Manual Sync ✓ ({syncData.Count} products, {totalPackings} packings)";
                        System.Diagnostics.Debug.WriteLine($"[{timestamp}] Manual sync completed successfully");
                    }
                    else
                    {
                        SyncStatusText = "Manual Sync ✓ (Up-to-date)";
                        System.Diagnostics.Debug.WriteLine($"[{timestamp}] No new products to sync");
                    }
                    SyncStatusColor = System.Windows.Media.Brushes.LimeGreen;
                    _lastSyncTime = DateTime.Now; // Reset the timer after manual sync
                }
                else
                {
                    SyncStatusText = "Offline";
                    SyncStatusColor = System.Windows.Media.Brushes.Red;
                    System.Diagnostics.Debug.WriteLine($"[{timestamp}] Manual sync returned null - check sync_log.txt for details");
                }
            }
            catch (Exception ex)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                string shortError = ex.Message.Length > 60 ? ex.Message.Substring(0, 60) + "..." : ex.Message;
                SyncStatusText = $"Sync Error: {shortError}";
                SyncStatusColor = System.Windows.Media.Brushes.Red;
                System.Diagnostics.Debug.WriteLine($"[{timestamp}] Manual Sync Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
    }
}
