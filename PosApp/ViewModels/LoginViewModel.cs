using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PosApp.Desktop.Models;
using PosApp.Desktop.Services;

namespace PosApp.Desktop.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private string _username = "";

        [ObservableProperty]
        private string _password = "";

        public event Action<Login, CounterInfo>? OnLoginSuccess;

        public AppSettings Settings => _settingsService.Settings;

        public LoginViewModel(IDataService dataService, ISettingsService settingsService)
        {
            _dataService = dataService;
            _settingsService = settingsService;
        }

        [RelayCommand]
        private async Task ValidateUsername()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                StatusText = "Please enter username.";
                return;
            }

            StatusText = "Checking username...";
            bool exists = await _dataService.UserExistsAsync(Username);

            if (exists)
            {
                StatusText = ""; // Clear status on success
            }
            else
            {
                StatusText = "Invalid username.";
            }
        }

        [RelayCommand]
        private async Task Login()
        {
            if (App.Current is App app && !app.IsInitialized)
            {
                StatusText = "System is still initializing. Please wait a second...";
                return;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                StatusText = "Please enter username.";
                return;
            }
            
            if (string.IsNullOrWhiteSpace(Password))
            {
                StatusText = "Please enter password.";
                return;
            }

            StatusText = "Logging in...";
            var user = await _dataService.AuthenticateAsync(Username, Password);
            if (user != null)
            {
                StatusText = "Login successful!";
                CounterInfo? counter = null;
                try
                {
                    counter = await _dataService.GetCounterAsync(_settingsService.Settings.CounterNo);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch counter info: {ex.Message}");
                }
                
                // Fallback if counter not found in DB or if fetch failed (e.g. schema mismatch)
                if (counter == null)
                {
                    counter = new CounterInfo 
                    { 
                        CounterNo = _settingsService.Settings.CounterNo,
                        CounterName = $"Counter {_settingsService.Settings.CounterNo:D2}",
                        SupervisorKey = "123"
                    };
                }

                OnLoginSuccess?.Invoke(user, counter);
                // Reset fields for next time
                Password = "";
                Username = "";
                StatusText = "";
            }
            else
            {
                // Determine specific error
                bool userExists = await _dataService.UserExistsAsync(Username);
                if (userExists)
                {
                    StatusText = "Invalid password.";
                }
                else
                {
                    StatusText = "Invalid username or password.";
                }
            }
        }
    }
}
