using CommunityToolkit.Mvvm.ComponentModel;

namespace PosApp.Desktop.ViewModels
{
    public partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private string _statusText = "Ready";

        /// <summary>
        /// Safely sets the owner of a dialog window to prevent crashes and ensure visibility.
        /// </summary>
        protected void SafelySetDialogOwner(System.Windows.Window dialog)
        {
            if (dialog == null) return;

            try
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                
                // CRITICAL: A window must be shown before it can be set as an owner.
                // We also MUST NOT set the owner to itself.
                if (mainWindow != null && mainWindow.IsVisible && mainWindow != dialog && mainWindow.IsLoaded)
                {
                    try
                    {
                        dialog.Owner = mainWindow;
                        
                        // If window is fullscreen or topmost, the dialog MUST be topmost too
                        if (mainWindow.Topmost || mainWindow.WindowStyle == System.Windows.WindowStyle.None)
                        {
                            dialog.Topmost = true;
                        }

                        dialog.ShowInTaskbar = false;
                        dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                    }
                    catch (System.InvalidOperationException ex)
                    {
                        // This usually happens if the window is in a state that doesn't allow setting an owner
                        System.Diagnostics.Debug.WriteLine($"InvalidOperationException setting dialog owner: {ex.Message}");
                        dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                    }
                }
                else
                {
                    // Fallback for standalone dialogs or when MainWindow is not ready
                    dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                    dialog.Topmost = true; 
                    
                    // If we are in kiosk mode (None style), we still want it to show over everything
                    if (mainWindow != null && mainWindow.WindowStyle == System.Windows.WindowStyle.None)
                    {
                        dialog.Topmost = true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not set dialog owner: {ex.Message}");
                // Last ditch fallback
                dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            }
        }
    }
}
