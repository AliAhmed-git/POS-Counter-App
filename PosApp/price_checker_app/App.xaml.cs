using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PosApp.Desktop.Services;
using PosApp.Desktop.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;

namespace PriceChecker
{
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }
        public static string ApiStatus { get; set; } = "API: INITIALIZING";

        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) => LogError($"Unhandled: {args.ExceptionObject}");
            DispatcherUnhandledException += (s, args) => LogError($"Dispatcher: {args.Exception.Message}");

            SetStartup();
            ConfigureServices();
            base.OnStartup(e);
            
            Task.Run(async () => {
                await EnsureDatabaseReady();
                await SyncDataOnStartup();
            });
        }

        private void LogError(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {message}\n");
            }
            catch { }
        }

        private async Task EnsureDatabaseReady()
        {
            try
            {
                var dataService = ServiceProvider?.GetRequiredService<IDataService>();
                if (dataService != null)
                {
                    await dataService.EnsureDatabaseCreatedAsync();
                    
                    // Ensure RPrice column exists in Items table
                    var contextFactory = ServiceProvider?.GetRequiredService<IDbContextFactory<PosDbContext>>();
                    if (contextFactory != null)
                    {
                        using var context = await contextFactory.CreateDbContextAsync();
                        try
                        {
                            await context.Database.ExecuteSqlRawAsync("ALTER TABLE Items ADD COLUMN RPrice DECIMAL(18,2) DEFAULT 0;");
                        }
                        catch (Exception)
                        {
                            // Ignore if already exists
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"DB Init Error: {ex.Message}");
            }
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            string dbPath = GetDatabasePath();
            services.AddDbContextFactory<PosDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            services.AddSingleton<IAppStatusService, AppStatusService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IDataService, DataService>();
            services.AddSingleton<ISyncService, SyncService>();
            services.AddSingleton<IScannerService, ScannerService>();
            services.AddTransient<PriceCheckerViewModel>();

            ServiceProvider = services.BuildServiceProvider();
        }

        private string GetDatabasePath()
        {
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(currentDir))
            {
                string path = Path.Combine(currentDir, "pos.db");
                if (File.Exists(path)) return path;
                
                string? parent = Directory.GetParent(currentDir)?.FullName;
                if (parent == currentDir) break;
                currentDir = parent ?? "";
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pos.db");
        }

        private async Task SyncDataOnStartup()
        {
            var statusService = ServiceProvider?.GetRequiredService<IAppStatusService>();
            try
            {
                if (statusService != null) statusService.ApiStatus = "API: SYNCING...";
                
                var syncService = ServiceProvider?.GetRequiredService<ISyncService>();
                var dataService = ServiceProvider?.GetRequiredService<IDataService>();
                var settingsService = ServiceProvider?.GetRequiredService<ISettingsService>();

                if (syncService != null && dataService != null && settingsService != null)
                {
                    int businessId = 41; // Corrected from Postman collection
                    int counterNo = 10; // Using 10 for faster server response
                    
                    var updatedItems = await syncService.DownloadUpdatedItemsAsync(businessId, counterNo);
                    if (updatedItems != null && updatedItems.Count > 0)
                    {
                        await dataService.SyncItemsAsync(updatedItems);
                        await syncService.ConfirmSyncAsync(businessId, counterNo);
                        if (statusService != null) statusService.ApiStatus = $"API: UPDATED ({updatedItems.Count} ITEMS)";
                    }
                    else
                    {
                        if (statusService != null) statusService.ApiStatus = "API: UP TO DATE";
                    }
                }
            }
            catch (Exception ex)
            {
                if (statusService != null) statusService.ApiStatus = "API: ERROR";
                LogError($"Startup Sync Error: {ex.Message}");
            }
        }

        private void SetStartup()
        {
            try
            {
                string? path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path)) return;

                RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (rk != null)
                {
                    rk.SetValue("PriceCheckerApp", path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set auto-start: {ex.Message}");
            }
        }
    }
}
