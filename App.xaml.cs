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

            volumeMonitor = new VolumeMonitor();
            volumeMonitor.VolumeChanged += OnVolumeChanged;

            if (Settings.Current.ShowTrayIcon)
            {
                trayIcon = new TrayIcon();
                trayIcon.Show();
                trayIcon.SettingsRequested += TrayIcon_SettingsRequested;
                trayIcon.ExitRequested += TrayIcon_ExitRequested;
            }

            // Show main window if not starting hidden
            if (!Settings.Current.StartHidden)
            {
                mainWindow.Show();
            }
        }

        private void OnVolumeChanged(int volumePercent)
        {
            try
            {
                // volumePercent is now already 0-100
                // Update tray icon (convert to 0-10 range for icon)
                trayIcon?.UpdateVolumeIcon(volumePercent / 10);

                // Update main window (keep as 0-100)
                if (mainWindow != null)
                {
                    mainWindow.Dispatcher.Invoke(() => 
                    {
                        mainWindow.UpdateVolume(volumePercent);
                    });
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
                volumeMonitor.Dispose();
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
