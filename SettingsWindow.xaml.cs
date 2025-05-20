using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using System.Windows.Data;
using System.Windows.Interop;
using System.Threading;
using System.Windows.Media.Animation;

namespace VolumeOSD
{
    public partial class SettingsWindow : Window
    {
        private MainWindow previewWindow;
        private bool isInitializing = true;
        private bool isPreviewVisible = false;
        private System.Windows.Threading.DispatcherTimer saveMessageTimer;
        private EventHandler previewWindowClosed;
        private RoutedEventHandler previewWindowLoaded;
        private bool IsClosing { get; set; }
        private HwndSource _hwndSource;
        private System.Windows.Threading.DispatcherTimer previewActivationTimer;
        private readonly object windowLock = new object();
        private readonly SynchronizationContext syncContext;
        private bool isSourceInitialized = false;
        private bool isDisposing = false;
        private bool isTestClickProcessing = false;

        public SettingsWindow()
        {
            InitializeComponent();
            this.Topmost = true;  // Keep settings window always on top
            syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
            DataContext = Settings.Current;

            Loaded += (s, e) =>
            {
                // Set initial position in ComboBox
                foreach (ComboBoxItem item in PositionSelector.Items)
                {
                    if (item.Content.ToString() == Settings.Current.Position)
                    {
                        PositionSelector.SelectedItem = item;
                        break;
                    }
                }
                isInitializing = false;
            };
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CleanupPreviewWindow();
            Hide();
        }

        private void TextColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new ColorDialog
            {
                Color = ColorFromString(Settings.Current.TextColor)
            };

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Current.TextColor = ColorToString(colorDialog.Color);
            }
        }

        private void BackgroundColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new ColorDialog
            {
                Color = ColorFromString(Settings.Current.BackgroundColor)
            };

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Current.BackgroundColor = ColorToString(colorDialog.Color);
            }
        }

        private void ProgressBarBackgroundColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new ColorDialog
            {
                Color = ColorFromString(Settings.Current.ProgressBarBackgroundColor)
            };

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Current.ProgressBarBackgroundColor = ColorToString(colorDialog.Color);
            }
        }

        private void ProgressBarForegroundColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new ColorDialog
            {
                Color = ColorFromString(Settings.Current.ProgressBarForegroundColor)
            };

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Current.ProgressBarForegroundColor = ColorToString(colorDialog.Color);
            }
        }

        private System.Drawing.Color ColorFromString(string colorString)
        {
            try
            {
                var wpfColor = (Color)ColorConverter.ConvertFromString(colorString);
                return System.Drawing.Color.FromArgb(wpfColor.R, wpfColor.G, wpfColor.B);
            }
            catch
            {
                return System.Drawing.Color.Black;
            }
        }

        private string ColorToString(System.Drawing.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private void PositionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || PositionSelector.SelectedItem == null) return;

            var selectedPosition = (PositionSelector.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (selectedPosition != null && selectedPosition != Settings.Current.Position)
            {
                Settings.Current.Position = selectedPosition;
                System.Diagnostics.Debug.WriteLine($"Position changed to: {selectedPosition}");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            Settings.Save();
            
            // Show the saved message
            SavedMessageText.Visibility = Visibility.Visible;
            
            // Start the animation
            var storyboard = (Storyboard)FindResource("FadeInOutStoryboard");
            storyboard.Begin(SavedMessageText);
            
            // Set up timer to hide the message
            if (saveMessageTimer != null)
            {
                saveMessageTimer.Stop();
                saveMessageTimer = null;
            }
            
            saveMessageTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            
            saveMessageTimer.Tick += (s, args) =>
            {
                SavedMessageText.Visibility = Visibility.Collapsed;
                SavedMessageText.Opacity = 0;
                saveMessageTimer.Stop();
                saveMessageTimer = null;
            };
            saveMessageTimer.Start();
        }
        
        private void SaveMessageTimer_Tick(object sender, EventArgs e)
        {
            SavedMessageText.Visibility = Visibility.Collapsed;
            saveMessageTimer.Stop();
            saveMessageTimer = null;
        }

        // Add Win32 constants and imports
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int HWND_TOPMOST = -1;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        private void InitializePreviewWindowHandlers()
        {
            // Store handlers as fields so we can properly remove them later
            previewWindowClosed = (s, args) =>
            {
                try
                {
                    Settings.Current.SetTemporaryShowOnPrimary(false);
                    isPreviewVisible = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error restoring ShowOnPrimary setting: {ex.Message}");
                }
                RemovePreviewWindowHandlers();
                previewWindow = null;
            };

            previewWindowLoaded = (s, args) =>
            {
                try
                {
                    if (!IsClosing && previewWindow != null)
                    {
                        lock (windowLock)
                        {
                            // Ensure window is properly positioned and visible
                            previewWindow.WindowState = WindowState.Normal;
                            previewWindow.Show();
                            
                            // Force update to ensure visibility
                            previewWindow.UpdateLayout();
                            
                            // Clean up any existing timer
                            if (previewActivationTimer != null)
                            {
                                previewActivationTimer.Stop();
                                previewActivationTimer = null;
                            }
                            
                            // Use a more reliable method to manage window z-order
                            previewActivationTimer = new System.Windows.Threading.DispatcherTimer
                            {
                                Interval = TimeSpan.FromMilliseconds(100)
                            };
                            
                            previewActivationTimer.Tick += (sender, e) =>
                            {
                                syncContext.Post(_ =>
                                {
                                    lock (windowLock)
                                    {
                                        previewActivationTimer.Stop();
                                        if (!IsClosing && previewWindow != null && previewWindow.IsLoaded)
                                        {
                                            try
                                            {
                                                // Set window styles to ensure proper z-order
                                                ApplyPreviewWindowStyles(previewWindow);
                                                
                                                // Ensure preview window is visible but doesn't steal focus
                                                if (previewWindow.Visibility != Visibility.Visible)
                                                {
                                                    previewWindow.Show();
                                                }
                                                
                                                // Set settings window as active
                                                Activate();
                                                Focus();
                                            }
                                            catch (Exception ex)
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Error setting window styles: {ex.Message}");
                                            }
                                        }
                                        previewActivationTimer = null;
                                    }
                                }, null);
                            };
                            previewActivationTimer.Start();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error ensuring preview window visibility: {ex.Message}");
                    if (previewActivationTimer != null)
                    {
                        previewActivationTimer.Stop();
                        previewActivationTimer = null;
                    }
                }
            };
        }

        private void RemovePreviewWindowHandlers()
        {
            if (previewWindow != null)
            {
                previewWindow.Closed -= previewWindowClosed;
                previewWindow.Loaded -= previewWindowLoaded;
            }
            previewWindowClosed = null;
            previewWindowLoaded = null;
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            // Prevent rapid clicking
            if (isTestClickProcessing)
            {
                System.Diagnostics.Debug.WriteLine("Test click ignored - previous click still processing");
                return;
            }

            if (!isSourceInitialized)
            {
                System.Diagnostics.Debug.WriteLine("Cannot create preview window - window source not initialized");
                return;
            }

            lock (windowLock)
            {
                isTestClickProcessing = true;
                try
                {
                    CleanupPreviewWindow();

                    // Enable temporary ShowOnPrimary for preview
                    Settings.Current.SetTemporaryShowOnPrimary(true);
                    
                    // Create a new preview window
                    previewWindow = new MainWindow();
                    isPreviewVisible = true;
                    
                    // Set window order and ensure visibility
                    previewWindow.Owner = this;  // Make settings window the owner
                    
                    // Initialize and attach event handlers
                    InitializePreviewWindowHandlers();
                    previewWindow.Loaded += previewWindowLoaded;
                    previewWindow.Closed += previewWindowClosed;
                    
                    // Before showing, ensure we update any debug logs for clarity
                    System.Diagnostics.Debug.WriteLine("TEST BUTTON PREVIEW: Creating preview window");
                    
                    // Show at 50% volume for preview
                    previewWindow.UpdateVolume(50);
                    
                    // Force settings window to top
                    if (!IsClosing)
                    {
                        Activate();
                        Focus();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in Test_Click: {ex.Message}");
                    // Clean up if something went wrong
                    CleanupPreviewWindow();
                    Settings.Current.SetTemporaryShowOnPrimary(false);
                    isPreviewVisible = false;
                }
                finally
                {
                    isTestClickProcessing = false;
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Check if the application is actually shutting down
            if (System.Windows.Application.Current.ShutdownMode == ShutdownMode.OnExplicitShutdown ||
                System.Windows.Application.Current.MainWindow == null)
            {
                // Application is actually closing, perform final cleanup
                e.Cancel = false;
                isDisposing = true;
                
                // Remove the window hook
                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource.Dispose();
                    _hwndSource = null;
                }
                
                FinalCleanup();
            }
            else
            {
                // Just hiding the window
                e.Cancel = true;
                CleanupPreviewWindow();
                Hide();
            }
        }

        public void ShowSettings()
        {
            lock (windowLock)
            {
                try
                {
                    // Clean up any existing preview window first
                    CleanupPreviewWindow();
                    
                    // Show and activate the window
                    Show();
                    WindowState = WindowState.Normal;
                    
                    // Force update layout
                    UpdateLayout();
                    
                    // Use synchronized activation to ensure proper focus
                    syncContext.Post(_ =>
                    {
                        try
                        {
                            if (!IsClosing)
                            {
                                // Set window styles
                                var helper = new WindowInteropHelper(this);
                                var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
                                SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle & ~WS_EX_NOACTIVATE); // Remove NOACTIVATE
                                
                                // Activate and focus
                                Activate();
                                Focus();
                                
                                // Ensure window is visible
                                if (Visibility != Visibility.Visible)
                                {
                                    Show();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error activating settings window: {ex.Message}");
                        }
                    }, null);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error showing settings window: {ex.Message}");
                }
            }
        }

        public void FinalCleanup()
        {
            try
            {
                // Ensure preview window is properly closed
                CleanupPreviewWindow();
                
                // Restore any temporary settings
                Settings.Current.SetTemporaryShowOnPrimary(false);
                
                // Save settings one last time to ensure all changes are persisted
                Settings.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during final cleanup: {ex.Message}");
            }
        }

        private void CleanupPreviewWindow()
        {
            lock (windowLock)
            {
                IsClosing = true;
                
                // Remove event handlers first
                RemovePreviewWindowHandlers();
                
                // Clean up preview window
                if (previewWindow != null)
                {
                    try
                    {
                        previewWindow.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error closing preview window: {ex.Message}");
                    }
                    previewWindow = null;
                }

                // Clean up activation timer
                if (previewActivationTimer != null)
                {
                    previewActivationTimer.Stop();
                    previewActivationTimer = null;
                }

                // Clean up save message timer
                if (saveMessageTimer != null)
                {
                    saveMessageTimer.Stop();
                    saveMessageTimer = null;
                }

                // Hide the saved message if it's visible
                if (SavedMessageText != null && SavedMessageText.Visibility == Visibility.Visible)
                {
                    SavedMessageText.Visibility = Visibility.Collapsed;
                }
                
                IsClosing = false;
            }
        }

        private void ApplyPreviewWindowStyles(Window window)
        {
            if (window == null || !window.IsLoaded) return;

            try
            {
                var helper = new WindowInteropHelper(window);
                var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
                SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
                
                // Set the window to be topmost without activating
                SetWindowPos(
                    helper.Handle,
                    (IntPtr)HWND_TOPMOST,
                    0, 0, 0, 0,
                    SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying window styles: {ex.Message}");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (_hwndSource != null)
            {
                try
                {
                    // Initialize window styles
                    var helper = new WindowInteropHelper(this);
                    var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
                    SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle & ~WS_EX_NOACTIVATE);
                    
                    // Add window hook
                    _hwndSource.AddHook(WndProc);
                    
                    // Set source initialized flag
                    isSourceInitialized = true;
                    
                    // Initialize message text
                    SavedMessageText.Opacity = 0;
                    
                    // Create the storyboard programmatically
                    var storyboard = (Storyboard)FindResource("FadeInOutStoryboard");
                    if (storyboard != null)
                    {
                        storyboard.Completed += (s, args) =>
                        {
                            if (SavedMessageText.Visibility == Visibility.Visible)
                            {
                                SavedMessageText.Visibility = Visibility.Collapsed;
                                SavedMessageText.Opacity = 0;
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in OnSourceInitialized: {ex.Message}");
                }
            }
        }

        private const int WM_ACTIVATE = 0x0006;
        private const int WM_ACTIVATEAPP = 0x001C;
        private const int WA_INACTIVE = 0;
        private const int WA_ACTIVE = 1;
        private const int WA_CLICKACTIVE = 2;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Skip all processing if we're disposing
            if (isDisposing)
            {
                return IntPtr.Zero;
            }

            if (!IsClosing && previewWindow != null && previewWindow.IsLoaded)
            {
                try
                {
                    switch (msg)
                    {
                        case WM_ACTIVATE:
                            int wParamLowWord = wParam.ToInt32() & 0xFFFF;
                            if (wParamLowWord == WA_ACTIVE || wParamLowWord == WA_CLICKACTIVE)
                            {
                                // Window is being activated, ensure preview window is visible
                                try
                                {
                                    if (!isDisposing && previewWindow?.Visibility != Visibility.Visible)
                                    {
                                        previewWindow?.Show();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error handling window activation: {ex.Message}");
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in WndProc: {ex.Message}");
                }
            }
            return IntPtr.Zero;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            
            lock (windowLock)
            {
                try
                {
                    if (previewWindow != null && previewWindow.IsLoaded)
                    {
                        switch (WindowState)
                        {
                            case WindowState.Minimized:
                                // Hide preview when settings is minimized
                                previewWindow.Hide();
                                break;
                                
                            case WindowState.Normal:
                            case WindowState.Maximized:
                                // Show preview when settings is restored/maximized
                                if (isPreviewVisible)
                                {
                                    previewWindow.Show();
                                    previewWindow.WindowState = WindowState.Normal;
                                    
                                    // Re-apply window styles
                                    ApplyPreviewWindowStyles(previewWindow);
                                }
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling window state change: {ex.Message}");
                }
            }
        }

        // Also add Deactivated event handler to ensure preview window stays visible
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            
            lock (windowLock)
            {
                try
                {
                    if (!IsClosing && previewWindow != null && previewWindow.IsLoaded && isPreviewVisible)
                    {
                        // Ensure preview window stays visible but doesn't steal focus
                        if (previewWindow.Visibility != Visibility.Visible)
                        {
                            previewWindow.Show();
                            previewWindow.WindowState = WindowState.Normal;
                        }
                        
                        // Re-apply window styles
                        ApplyPreviewWindowStyles(previewWindow);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling window deactivation: {ex.Message}");
                }
            }
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            
            lock (windowLock)
            {
                try
                {
                    if (!IsClosing && previewWindow != null && previewWindow.IsLoaded && isPreviewVisible)
                    {
                        // Ensure preview window styles and visibility
                        ApplyPreviewWindowStyles(previewWindow);
                        
                        // Ensure window is visible
                        if (previewWindow.Visibility != Visibility.Visible)
                        {
                            previewWindow.Show();
                            previewWindow.WindowState = WindowState.Normal;
                        }
                        
                        // Force layout update to ensure proper positioning
                        previewWindow.UpdateLayout();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling window location change: {ex.Message}");
                }
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            
            lock (windowLock)
            {
                try
                {
                    if (!IsClosing && previewWindow != null && previewWindow.IsLoaded && isPreviewVisible)
                    {
                        // Ensure preview window handles DPI change properly
                        var helper = new WindowInteropHelper(previewWindow);
                        
                        // Re-apply window styles and ensure visibility
                        ApplyPreviewWindowStyles(previewWindow);
                        
                        // Force window to recalculate its layout
                        previewWindow.InvalidateMeasure();
                        previewWindow.InvalidateVisual();
                        previewWindow.UpdateLayout();
                        
                        // Ensure window is visible with correct state
                        if (previewWindow.Visibility != Visibility.Visible)
                        {
                            previewWindow.Show();
                            previewWindow.WindowState = WindowState.Normal;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling DPI change: {ex.Message}");
                }
            }
        }
    }

}
