using System;
using System.Windows;

namespace VolumeOSD
{
    public partial class App : Application
    {
        private TrayIcon trayIcon;
        private VolumeMonitor volumeMonitor;
        private MainWindow mainWindow;
        private SettingsWindow settingsWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Settings.Load();

            // Create MainWindow first (but don't show it yet)
            mainWindow = new MainWindow();
            System.Diagnostics.Debug.WriteLine("Main window created - will be shown when volume changes");

            // Initialize volume monitor
            volumeMonitor = new VolumeMonitor();
            volumeMonitor.VolumeChanged += OnVolumeChanged;
            System.Diagnostics.Debug.WriteLine("Volume monitor initialized and connected");

            if (Settings.Current.ShowTrayIcon)
            {
                trayIcon = new TrayIcon();
                trayIcon.Show();
                trayIcon.SettingsRequested += TrayIcon_SettingsRequested;
                trayIcon.MainWindowToggleRequested += TrayIcon_MainWindowToggleRequested;
                trayIcon.ExitRequested += TrayIcon_ExitRequested;
                System.Diagnostics.Debug.WriteLine("Tray icon initialized and shown");
            }
            
            // Note: We don't show the main window initially anymore
            // It will be shown automatically when volume changes
        }

        private void OnVolumeChanged(int volumePercent)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Volume changed to {volumePercent}%");
                
                // volumePercent is now already 0-100
                // Update tray icon (convert to 0-10 range for icon)
                trayIcon?.UpdateVolumeIcon(volumePercent / 10);

                // Update main window (keep as 0-100)
                if (mainWindow != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Updating main window with volume: {volumePercent}%");
                    mainWindow.Dispatcher.Invoke(() => 
                    {
                        System.Diagnostics.Debug.WriteLine($"Main window visible before update: {mainWindow.IsVisible}");
                        mainWindow.UpdateVolume(volumePercent);
                        System.Diagnostics.Debug.WriteLine($"Main window visible after update: {mainWindow.IsVisible}");
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Main window is null when trying to update volume");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating volume: {ex.Message}");
            }
        }

        private void TrayIcon_SettingsRequested(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (settingsWindow == null)
                {
                    settingsWindow = new SettingsWindow();
                }

                if (!settingsWindow.IsVisible)
                {
                    settingsWindow.Show();
                    settingsWindow.Activate();
                }
                else
                {
                    settingsWindow.Activate();
                }
            });
        }
        
        private void TrayIcon_MainWindowToggleRequested(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (mainWindow != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Toggling main window visibility. Current IsVisible: {mainWindow.IsVisible}");
                    
                    if (mainWindow.IsVisible)
                    {
                        // Hide the window if it's currently visible
                        System.Diagnostics.Debug.WriteLine("Main window is visible, hiding it");
                        mainWindow.Hide();
                    }
                    else
                    {
                        // Show the window with current volume if it's hidden
                        System.Diagnostics.Debug.WriteLine("Main window is hidden, showing it");
                        
                        // Use most recent volume level to update the window
                        try
                        {
                            if (volumeMonitor != null)
                            {
                                // Force a volume update to refresh the window
                                volumeMonitor.ForceVolumeNotification();
                            }
                            else
                            {
                                // Show with a default volume if monitor isn't available
                                mainWindow.UpdateVolume(50);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error updating volume for toggle: {ex.Message}");
                            mainWindow.UpdateVolume(50);  // Fallback to 50% if there's an error
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"After toggle, IsVisible: {mainWindow.IsVisible}");
                }
            });
        }

        private void TrayIcon_ExitRequested(object sender, EventArgs e)
        {
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Settings.Save();

            if (mainWindow != null)
            {
                mainWindow.Close();
                mainWindow = null;
            }

            if (settingsWindow != null)
            {
                settingsWindow.Close();
                settingsWindow = null;
            }

            if (volumeMonitor != null)
            {
                try
                {
                    volumeMonitor.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing volume monitor: {ex.Message}");
                }
                volumeMonitor = null;
            }

            if (trayIcon != null)
            {
                trayIcon.Dispose();
                trayIcon = null;
            }

            base.OnExit(e);
        }
    }
}
