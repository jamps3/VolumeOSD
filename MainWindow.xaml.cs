using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using System.Linq;

namespace VolumeOSD
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer hideTimer;
        private bool isUpdatingPosition = false;

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize timer
            hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Settings.Current.DisplayDuration)
            };
            hideTimer.Tick += HideTimer_Tick;

            // Initial values from settings
            ApplySettings();

            // Listen for settings changes
            Settings.Current.PropertyChanged += Settings_PropertyChanged;
            
            // Hide window initially
            Hide();

            // Initial position update when loaded
            this.Loaded += (s, e) => UpdateSizeAndPosition();
        }

        private void ApplySettings()
        {
            try
            {
                hideTimer.Interval = TimeSpan.FromSeconds(Settings.Current.DisplayDuration);
                Opacity = (100 - Settings.Current.Transparency) / 100.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying settings: {ex.Message}");
            }
        }

        private void UpdateSizeAndPosition()
        {
            if (isUpdatingPosition) return;
            isUpdatingPosition = true;

            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                var targetScreen = Settings.Current.ShowOnPrimary ? 
                    System.Windows.Forms.Screen.PrimaryScreen : 
                    screens.FirstOrDefault() ?? System.Windows.Forms.Screen.PrimaryScreen;

                var workingArea = targetScreen.WorkingArea;
                System.Diagnostics.Debug.WriteLine($"Updating position to: {Settings.Current.Position}");
                System.Diagnostics.Debug.WriteLine($"Screen working area: Left={workingArea.Left}, Top={workingArea.Top}, Width={workingArea.Width}, Height={workingArea.Height}");

                this.UpdateLayout();

                double left = workingArea.Left, top = workingArea.Top;
                switch (Settings.Current.Position)
                {
                    case "Top Left":
                        left = workingArea.Left + 10;
                        top = workingArea.Top + 10;
                        break;
                    case "Top Center":
                        left = workingArea.Left + (workingArea.Width - ActualWidth) / 2;
                        top = workingArea.Top + 10;
                        break;
                    case "Top Right":
                        left = workingArea.Right - ActualWidth - 10;
                        top = workingArea.Top + 10;
                        break;
                    case "Middle Left":
                        left = workingArea.Left + 10;
                        top = workingArea.Top + (workingArea.Height - ActualHeight) / 2;
                        break;
                    case "Middle Center":
                        left = workingArea.Left + (workingArea.Width - ActualWidth) / 2;
                        top = workingArea.Top + (workingArea.Height - ActualHeight) / 2;
                        break;
                    case "Middle Right":
                        left = workingArea.Right - ActualWidth - 10;
                        top = workingArea.Top + (workingArea.Height - ActualHeight) / 2;
                        break;
                    case "Bottom Left":
                        left = workingArea.Left + 10;
                        top = workingArea.Bottom - ActualHeight - 10;
                        break;
                    case "Bottom Center":
                        left = workingArea.Left + (workingArea.Width - ActualWidth) / 2;
                        top = workingArea.Bottom - ActualHeight - 10;
                        break;
                    case "Bottom Right":
                        left = workingArea.Right - ActualWidth - 10;
                        top = workingArea.Bottom - ActualHeight - 10;
                        break;
                }

                Left = left;
                Top = top;

                System.Diagnostics.Debug.WriteLine($"Window position set to: Left={Left}, Top={Top}");
            }
            finally
            {
                isUpdatingPosition = false;
            }
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Settings changed: {e.PropertyName}");
            
            if (e.PropertyName == nameof(Settings.Position) || e.PropertyName == nameof(Settings.ShowOnPrimary))
            {
                UpdateSizeAndPosition();
            }
            else
            {
                ApplySettings();
            }
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            hideTimer.Stop();
            Hide();
        }


        public void UpdateVolume(int volumePercent)
        {
            volumePercent = Math.Max(0, Math.Min(100, volumePercent));

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                VolumeProgressBar.Value = volumePercent;
                VolumePercentText.Text = volumePercent + "%";
                
                if (!IsVisible)
                {
                    UpdateSizeAndPosition();
                    Show();
                }
            });
            
            hideTimer.Stop();
            hideTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            Settings.Current.PropertyChanged -= Settings_PropertyChanged;
            hideTimer.Stop();
            base.OnClosed(e);
        }
    }
}
