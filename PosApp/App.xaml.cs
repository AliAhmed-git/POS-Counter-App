using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PosApp.Desktop.Data;
using PosApp.Desktop.Services;
using PosApp.Desktop.ViewModels;

namespace PosApp.Desktop
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogException(e.ExceptionObject as Exception, "AppDomain");
            DispatcherUnhandledException += (s, e) => { LogException(e.Exception, "Dispatcher"); e.Handled = true; };

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        private void LogException(Exception? ex, string source)
        {
            if (ex == null) return;
            string log = $"[{DateTime.Now}] CRASH ({source}): {ex.Message}\nStack: {ex.StackTrace}\nInner: {ex.InnerException?.Message}\n\n";
            File.AppendAllText("crash_log.txt", log);
            DebugLog($"CRASH ({source}): {ex.Message}");
            MessageBox.Show($"A fatal error occurred. See crash_log.txt for details.\n\n{ex.Message}", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void DebugLog(string message)
        {
            try { File.AppendAllText("debug_log.txt", $"[{DateTime.Now}] {message}\n"); } catch { }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Database
            services.AddDbContextFactory<PosDbContext>(options =>
                options.UseSqlite("Data Source=C:\\xampp\\htdocs\\pos-counter\\PosApp\\pos.db"));

            // Services
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IDataService, DataService>();
            services.AddSingleton<IPrintService, ModernPrintService>();
            services.AddSingleton<IScannerService, ScannerService>();
            services.AddSingleton<ISyncService, SyncService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<LoginViewModel>();
            services.AddSingleton<SaleViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<DCRViewModel>();

            // Windows
            services.AddSingleton<MainWindow>();
        }

        public bool IsInitialized { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            DebugLog("App Startup Initiated");
            // 1. Show splash screen first
            Views.SplashScreen? splash = null;
            try
            {
                splash = new Views.SplashScreen();
                splash.Show();
                DebugLog("Splash Screen Shown");
            }
            catch (Exception ex)
            {
                DebugLog($"Splash Screen Error: {ex.Message}");
            }

            // 2. Run initialization in background but handle it linearly
            _ = Task.Run(async () =>
            {
                DebugLog("Background Initialization Started");
                try
                {
                    // Initialization logic
                    var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
                    await settingsService.LoadSettingsAsync();
                    DebugLog("Settings Loaded");

                    var dataService = ServiceProvider.GetRequiredService<IDataService>();
                    DebugLog("Calling EnsureDatabaseCreatedAsync");
                    await dataService.EnsureDatabaseCreatedAsync();
                    DebugLog("EnsureDatabaseCreatedAsync returned");

                    IsInitialized = true;
                    DebugLog("IsInitialized set to true");

                    // Wait minimum time for splash
                    await Task.Delay(2000);
                    DebugLog("Splash delay completed, showing MainWindow");

                    await Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            DebugLog("Resolving MainWindow");
                            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                            DebugLog("MainWindow resolved");
                            Application.Current.MainWindow = mainWindow;
                            splash?.Close();
                            DebugLog("Splash closed");
                            mainWindow.Show();
                            DebugLog("MainWindow.Show() called");
                        }
                        catch (Exception ex)
                        {
                            DebugLog($"MainWindow Show Error: {ex.Message}\n{ex.StackTrace}");
                            throw;
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogException(ex, "Initialization");
                    await Dispatcher.InvokeAsync(() => 
                    {
                        splash?.Close();
                        // Even if init fails, we might need a window to report it or try to recover
                        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                        mainWindow.Show();
                    });
                }
            });

            base.OnStartup(e);
        }
    }
}
