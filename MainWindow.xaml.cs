using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.IO;

namespace VolumeOSD
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Win32 constants for window positioning
        private const int HWND_TOPMOST = -1;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_SHOWWINDOW = 0x0040;
        
        // Window style constants
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        
        // Win32 imports for window positioning and styles
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("kernel32.dll")]
        private static extern int GetLastError();
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        private readonly DispatcherTimer hideTimer;
        private bool isUpdatingPosition = false;
        private int volume;
        private System.Windows.Forms.Screen lastUsedScreen = null; // Track the last used screen
        private static readonly string logFilePath = "VolumeOSD_debug.log";
        private static readonly bool enableFileLogging = true;
        
        public int Volume
        {
            get => volume;
            set
            {
                if (volume != value)
                {
                    volume = value;
                    OnPropertyChanged(nameof(Volume));
                }
            }
        }

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
            
            // Log current progress bar settings
            LogProgressBarSettings();
            
            // Setup value change notification for progress bar
            VolumeProgressBar.ValueChanged += (s, e) => 
            {
                System.Diagnostics.Debug.WriteLine($"Progress bar value changed: {e.NewValue}");
            };
            
            // Log window properties
            System.Diagnostics.Debug.WriteLine($"Window initialization - " +
                $"WindowStyle: {WindowStyle}, " +
                $"AllowsTransparency: {AllowsTransparency}, " +
                $"Topmost: {Topmost}, " +
                $"Background: {Background}, " +
                $"Width: {Width}, " +
                $"Height: {Height}, " +
                $"Visibility: {Visibility}, " +
                $"Opacity: {Opacity}");
            
            // Hide window initially
            Hide();
            System.Diagnostics.Debug.WriteLine("Window hidden initially");

            // Initial position update when loaded
            this.Loaded += (s, e) => 
            {
                UpdateSizeAndPosition();
                EnsureAlwaysOnTop();
            };
            
            // Handle window activation to stay on top of fullscreen apps
            this.Activated += (s, e) => EnsureAlwaysOnTop();
        }
        
        /// <summary>
        /// Ensures the window stays on top of all applications including fullscreen apps
        /// </summary>
        private void EnsureAlwaysOnTop()
        {
            // Get handle for this window
            var windowHandle = new WindowInteropHelper(this).Handle;
            
            System.Diagnostics.Debug.WriteLine($"Setting window always on top - Handle: {windowHandle}");
            
            try
            {
                // Get current extended window style
                int exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
                System.Diagnostics.Debug.WriteLine($"Current extended window style: 0x{exStyle:X8}");
                
                // Add TOPMOST, TOOLWINDOW, and NOACTIVATE styles
                exStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                
                // Set the new extended window style
                int result = SetWindowLong(windowHandle, GWL_EXSTYLE, exStyle);
                
                if (result == 0)
                {
                    int error = GetLastError();
                    System.Diagnostics.Debug.WriteLine($"Error setting window style. Error code: {error}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Window style set to: 0x{exStyle:X8}");
                }
                
                // Set window to be topmost with special flags to ensure it's above fullscreen apps
                bool posResult = SetWindowPos(
                    windowHandle,                           // Window handle
                    (IntPtr)HWND_TOPMOST,                   // Always on top
                    0, 0,                                   // Ignore position
                    0, 0,                                   // Ignore size
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW  // Flags
                );
                
                if (!posResult)
                {
                    int error = GetLastError();
                    System.Diagnostics.Debug.WriteLine($"Error setting window position. Error code: {error}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Window position set successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring window is always on top: {ex.Message}");
            }
            
            // Ensure the Topmost property is set (belt and suspenders)
            this.Topmost = true;
            System.Diagnostics.Debug.WriteLine($"Window topmost set to: {Topmost}");
        }
        
        /// <summary>
        /// Forces the window to be visible and active
        /// </summary>
        private void EnsureWindowVisible()
        {
            System.Diagnostics.Debug.WriteLine($"Ensuring window visibility - Before: {Visibility}, State: {WindowState}");
            
            // Reset window state to normal (in case it was minimized)
            WindowState = WindowState.Normal;
            
            // Set visibility to Visible
            Visibility = Visibility.Visible;
            
            // Force layout update
            UpdateLayout();
            
            // Activate window to bring it to front
            Activate();
            
            System.Diagnostics.Debug.WriteLine($"Window visibility ensured - After: {Visibility}, State: {WindowState}");
            System.Diagnostics.Debug.WriteLine($"Window dimensions - ActualWidth: {ActualWidth}, ActualHeight: {ActualHeight}");
        }

        private void ApplySettings()
        {
            try
            {
                hideTimer.Interval = TimeSpan.FromSeconds(Settings.Current.DisplayDuration);
                Opacity = (100 - Settings.Current.Transparency) / 100.0;
                
                // Log text color and other color settings
                LogColorSettings();
                
                // Log progress bar color settings
                LogProgressBarSettings();
                
                // Explicitly set progress bar colors in code in addition to XAML bindings
                try
                {
                    // Create converter to convert color strings to actual colors
                    var converter = new StringToColorConverter();
                    
                    // Get progress bar background and foreground colors from settings
                    var bgColor = (Color)converter.Convert(Settings.Current.ProgressBarBackgroundColor, typeof(Color), null, null);
                    var fgColor = (Color)converter.Convert(Settings.Current.ProgressBarForegroundColor, typeof(Color), null, null);
                    
                    System.Diagnostics.Debug.WriteLine($"Setting progress bar colors in code:");
                    System.Diagnostics.Debug.WriteLine($"  Background: {Settings.Current.ProgressBarBackgroundColor} -> {bgColor}");
                    System.Diagnostics.Debug.WriteLine($"  Foreground: {Settings.Current.ProgressBarForegroundColor} -> {fgColor}");
                    
                    // Find the track and indicator borders in the template
                    if (VolumeProgressBar.Template != null)
                    {
                        // Force template regeneration and application
                        VolumeProgressBar.ApplyTemplate();
                        
                        // Try to find PART_Track and PART_Indicator in the template
                        if (VolumeProgressBar.Template.FindName("PART_Track", VolumeProgressBar) is System.Windows.Controls.Border track)
                        {
                            track.Background = new SolidColorBrush(bgColor);
                            System.Diagnostics.Debug.WriteLine($"  PART_Track background set to {bgColor}");
                        }
                        
                        if (VolumeProgressBar.Template.FindName("PART_Indicator", VolumeProgressBar) is System.Windows.Controls.Border indicator)
                        {
                            indicator.Background = new SolidColorBrush(fgColor);
                            System.Diagnostics.Debug.WriteLine($"  PART_Indicator background set to {fgColor}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Error: ProgressBar template is null");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting progress bar colors: {ex.Message}");
                }
                
                // Ensure progress bar is visible and has non-zero size
                System.Diagnostics.Debug.WriteLine($"ProgressBar visibility: {VolumeProgressBar.Visibility}, " +
                                                  $"Width: {VolumeProgressBar.Width}, " +
                                                  $"Height: {VolumeProgressBar.Height}, " +
                                                  $"ActualWidth: {VolumeProgressBar.ActualWidth}, " +
                                                  $"ActualHeight: {VolumeProgressBar.ActualHeight}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying settings: {ex.Message}");
            }
        }
        
        private void LogColorSettings()
        {
            System.Diagnostics.Debug.WriteLine($"Window Color Settings:");
            System.Diagnostics.Debug.WriteLine($"  Text Color: {Settings.Current.TextColor}");
            System.Diagnostics.Debug.WriteLine($"  Background Color: {Settings.Current.BackgroundColor}");
            System.Diagnostics.Debug.WriteLine($"  Transparency: {Settings.Current.Transparency}%");
            
            // Log actual text block foreground
            var textBrush = VolumePercentText.Foreground as SolidColorBrush;
            System.Diagnostics.Debug.WriteLine($"  Text Block Foreground: {(textBrush != null ? textBrush.Color.ToString() : "not a SolidColorBrush")}");
            System.Diagnostics.Debug.WriteLine($"  Text Block Opacity: {VolumePercentText.Opacity}");
            
            try
            {
                // Apply text color using centralized method
                ApplyTextColor();
                
                // Verify after setting
                textBrush = VolumePercentText.Foreground as SolidColorBrush;
                System.Diagnostics.Debug.WriteLine($"  Text Block Foreground after setting: {(textBrush != null ? textBrush.Color.ToString() : "not a SolidColorBrush")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"  Error setting text color: {ex.Message}");
            }
        }
        
        private void LogProgressBarSettings()
        {
            System.Diagnostics.Debug.WriteLine($"Progress Bar Settings:");
            System.Diagnostics.Debug.WriteLine($"  Background Color: {Settings.Current.ProgressBarBackgroundColor}");
            System.Diagnostics.Debug.WriteLine($"  Foreground Color: {Settings.Current.ProgressBarForegroundColor}");
            System.Diagnostics.Debug.WriteLine($"  Current Volume: {Volume}%");
        }

        /// <summary>
        /// Logs a message to console, debug, and optionally to a file
        /// </summary>
        private static void Log(string message, bool important = false)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string formattedMessage = $"[{timestamp}] {message}";
            
            // Always log to Debug
            System.Diagnostics.Debug.WriteLine(formattedMessage);
            
            // Log important messages to console
            if (important)
            {
                Console.WriteLine(formattedMessage);
            }
            
            // Optionally log to file
            if (enableFileLogging)
            {
                try
                {
                    // Append to log file
                    using (StreamWriter writer = new StreamWriter(logFilePath, true))
                    {
                        writer.WriteLine(formattedMessage);
                    }
                }
                catch
                {
                    // Ignore file writing errors
                }
            }
        }
        
        private void UpdateSizeAndPosition()
        {
            if (isUpdatingPosition) return;
            isUpdatingPosition = true;

            try
            {
                // Get all screens in the system
                var screens = System.Windows.Forms.Screen.AllScreens;
                Log($"Found {screens.Length} screen(s)", true);
                
                // Log all screens info for better debugging
                for (int i = 0; i < screens.Length; i++)
                {
                    var screen = screens[i];
                    Log($"Screen {i}: DeviceName={screen.DeviceName}, Primary={screen.Primary}", true);
                }
                
                // Log primary screen info
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                Log($"Primary screen: DeviceName={primaryScreen.DeviceName}", true);
                Log($"Last used screen: {(lastUsedScreen != null ? lastUsedScreen.DeviceName : "None")}", true);
                
                // Determine target screen based on settings
                System.Windows.Forms.Screen targetScreen;
                
                // Simplified handling for single-screen case
                if (screens.Length == 1)
                {
                    // If only one screen is available, use it regardless of ShowOnPrimary setting
                    targetScreen = screens[0];
                    lastUsedScreen = targetScreen;
                    
                    // Still log the setting for debugging
                    Log($"Single screen environment - Using only available screen: {targetScreen.DeviceName} (ShowOnPrimary={Settings.Current.ShowOnPrimary})", true);
                }
                else if (Settings.Current.ShowOnPrimary)
                {
                    // Always use primary screen when ShowOnPrimary is true (multi-screen environment)
                    targetScreen = primaryScreen;
                    Log("ShowOnPrimary=true: Using PRIMARY screen per settings", true);
                }
                else
                {
                    // Get current screen based on visibility status and history (multi-screen environment)
                    System.Windows.Forms.Screen currentScreen;
                    
                    if (IsVisible)
                    {
                        // Get window's current screen from its position
                        var windowHandle = new WindowInteropHelper(this).Handle;
                        currentScreen = System.Windows.Forms.Screen.FromHandle(windowHandle);
                        System.Diagnostics.Debug.WriteLine($"Window visible, getting screen from window handle: {windowHandle}");
                    }
                    else if (lastUsedScreen != null)
                    {
                        // Use last used screen if available and still connected
                        var foundScreen = screens.FirstOrDefault(s => s.DeviceName == lastUsedScreen.DeviceName);
                        if (foundScreen != null)
                        {
                            currentScreen = foundScreen;
                            System.Diagnostics.Debug.WriteLine($"Window not visible, using last used screen: {currentScreen.DeviceName}");
                        }
                        else
                        {
                            // Last used screen no longer available, fall back to primary
                            currentScreen = primaryScreen;
                            System.Diagnostics.Debug.WriteLine("Last used screen no longer available, falling back to primary");
                        }
                    }
                    else
                    {
                        // No history, start on primary screen by default
                        currentScreen = primaryScreen;
                        System.Diagnostics.Debug.WriteLine("No screen history, using primary screen by default");
                    }
                    
                    targetScreen = currentScreen;
                    bool isCurrentPrimary = Object.ReferenceEquals(currentScreen, primaryScreen);
                    Log($"ShowOnPrimary=false: Using {(isCurrentPrimary ? "PRIMARY" : "SECONDARY")} screen, DeviceName={currentScreen.DeviceName}", true);
                }
                
                // Save the selected screen for future reference
                lastUsedScreen = targetScreen;
                
                // Get working area of the selected screen
                var workingArea = targetScreen.WorkingArea;
                
                System.Diagnostics.Debug.WriteLine($"Updating position to: {Settings.Current.Position}");
                System.Diagnostics.Debug.WriteLine($"Target screen: Bounds={targetScreen.Bounds}, WorkingArea={workingArea}, IsPrimary={targetScreen == primaryScreen}");

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
                
                // Calculate window width and height (use ActualWidth/Height if available, otherwise use Width/Height)
                double windowWidth = ActualWidth > 1 ? ActualWidth : Width;
                double windowHeight = ActualHeight > 1 ? ActualHeight : Height;
                
                // Ensure coordinates are within screen bounds
                left = Math.Max(workingArea.Left, Math.Min(left, workingArea.Right - windowWidth));
                top = Math.Max(workingArea.Top, Math.Min(top, workingArea.Bottom - windowHeight));
                
                // Log position calculation
                System.Diagnostics.Debug.WriteLine($"Position calculation: " +
                                                 $"Window dimensions: {windowWidth}x{windowHeight}, " +
                                                 $"Calculated position: ({left},{top}), " + 
                                                 $"Screen working area: {workingArea}");

                // Apply the calculated position
                Left = left;
                Top = top;

                // Log final position
                System.Diagnostics.Debug.WriteLine($"Window position set to: Left={Left}, Top={Top} on " +
                                                 $"Screen: {(Settings.Current.ShowOnPrimary ? "Primary" : "Current")}, " +
                                                 $"Position: {Settings.Current.Position}, " +
                                                 $"On screen: {targetScreen.DeviceName}");
                
                Log($"Window positioned at: ({Left},{Top}) on screen: {targetScreen.DeviceName}, Position: {Settings.Current.Position}, ShowOnPrimary: {Settings.Current.ShowOnPrimary}", true);
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
                Log($"Position or ShowOnPrimary changed: Position={Settings.Current.Position}, ShowOnPrimary={Settings.Current.ShowOnPrimary}", true);
                UpdateSizeAndPosition();
            }
            else if (e.PropertyName == nameof(Settings.TextColor))
            {
                System.Diagnostics.Debug.WriteLine($"Text color changed: {Settings.Current.TextColor}");
                
                // Use centralized method to apply text color
                ApplyTextColor();
                
                // Force layout update to ensure color is applied
                VolumePercentText.UpdateLayout();
                
                // Apply other settings
                ApplySettings();
                
                // Force a second color application after settings
                ApplyTextColor();
            }
            else if (e.PropertyName == nameof(Settings.ProgressBarBackgroundColor) || 
                     e.PropertyName == nameof(Settings.ProgressBarForegroundColor))
            {
                System.Diagnostics.Debug.WriteLine($"Progress bar color changed: {e.PropertyName}");
                LogProgressBarSettings();
                
                // Force progress bar style refresh
                VolumeProgressBar.Style = null;
                VolumeProgressBar.Style = this.FindResource("CustomProgressBar") as Style;
                
                // Force template reapplication
                VolumeProgressBar.ApplyTemplate();
                
                // Apply settings (which will also explicitly set colors)
                ApplySettings();
                
                // Force layout update to ensure changes are visible
                VolumeProgressBar.UpdateLayout();
                
                System.Diagnostics.Debug.WriteLine("Progress bar style and template refreshed");
            }
            else
            {
                ApplySettings();
            }
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            hideTimer.Stop();
            System.Diagnostics.Debug.WriteLine("Hiding window after timer elapsed");
            Hide();
            System.Diagnostics.Debug.WriteLine($"After Hide() - IsVisible={IsVisible}, Visibility={Visibility}");
        }

        public void UpdateVolume(int volumePercent)
        {
            volumePercent = Math.Max(0, Math.Min(100, volumePercent));

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine($"Updating volume to: {volumePercent}%");
                System.Diagnostics.Debug.WriteLine($"Window visibility before update: IsVisible={IsVisible}, Visibility={Visibility}, WindowState={WindowState}");
                
                // Apply text color BEFORE setting the text content
                ApplyTextColor();
                
                // Update volume and text
                Volume = volumePercent;
                VolumePercentText.Text = volumePercent + "%";
                
                // Handle mute state (0% volume) visibility
                bool isMuted = (volumePercent == 0);
                System.Diagnostics.Debug.WriteLine($"Volume state: {(isMuted ? "MUTED (0%)" : volumePercent + "%")}");
                
                // Check element visibility matches volume state
                if (MuteSymbol.Visibility != (isMuted ? Visibility.Visible : Visibility.Collapsed))
                {
                    System.Diagnostics.Debug.WriteLine($"Refreshing visibility states for volume: {volumePercent}%");
                }
                
                // Force layout update to ensure visibility changes are applied
                UpdateLayout();
                
                // Apply text color again to ensure it's set
                ApplyTextColor();
                
                // Explicitly set progress bar value
                VolumeProgressBar.Value = volumePercent;
                System.Diagnostics.Debug.WriteLine($"Progress bar value set to: {VolumeProgressBar.Value}");
                
                // Check if we're being called from the Test button in settings (special case)
                bool isTestButtonPreview = new System.Diagnostics.StackTrace().ToString().Contains("Test_Click");
                

                // Make sure window is visible
                if (!IsVisible)
                {
                    // Check if we're on primary display
                    var screens = System.Windows.Forms.Screen.AllScreens;
                    var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                    bool isOnPrimaryDisplay = (screens.Length == 1) || 
                                              (lastUsedScreen != null && lastUsedScreen.DeviceName == primaryScreen.DeviceName);
                    
                    // Determine if we should show the window based on conditions:
                    bool shouldShow = 
                        // Case 1: Always show if test button is pressed
                        isTestButtonPreview || 
                        // Case 2: Show if ShowOnPrimary is true AND we're on primary display
                        (Settings.Current.ShowOnPrimary && isOnPrimaryDisplay) ||
                        // Case 3: Show if we're on a non-primary display (regardless of ShowOnPrimary)
                        !isOnPrimaryDisplay;
                    
                    // Log the decision process
                    Log($"Window display decision: " +
                        $"ShowOnPrimary={Settings.Current.ShowOnPrimary}, " +
                        $"IsTestButtonPreview={isTestButtonPreview}, " +
                        $"IsOnPrimaryDisplay={isOnPrimaryDisplay}, " + 
                        $"LastUsedScreen={lastUsedScreen?.DeviceName ?? "None"}, " +
                        $"DECISION: {(shouldShow ? "SHOW" : "HIDE")}", true);
                    
                    // Only show if we meet one of the conditions above
                    if (shouldShow)
                    {
                        System.Diagnostics.Debug.WriteLine($"Window not visible, showing it now (ShowOnPrimary={Settings.Current.ShowOnPrimary})");
                        // Log screen selection info before updating position
                        Log($"Showing window - ShowOnPrimary={Settings.Current.ShowOnPrimary}, LastUsedScreen={lastUsedScreen?.DeviceName ?? "None"}", true);
                        
                        // Ensure position is updated first using the correct screen
                        UpdateSizeAndPosition();
                    
                    // Reset window state and show window
                    WindowState = WindowState.Normal;
                    Show();
                    
                    // Force layout update and ensure always on top
                    UpdateLayout();
                    EnsureAlwaysOnTop();
                    
                    // Force activation and focus
                    Activate();
                    Focus();
                    
                    // Ensure proper visibility
                    EnsureWindowVisible();
                    
                    // Verify window is now visible
                    System.Diagnostics.Debug.WriteLine($"After Show() - IsVisible={IsVisible}, Visibility={Visibility}, WindowState={WindowState}");
                    System.Diagnostics.Debug.WriteLine($"Window dimensions - ActualWidth: {ActualWidth}, ActualHeight: {ActualHeight}");
                    System.Diagnostics.Debug.WriteLine($"ProgressBar - ActualWidth={VolumeProgressBar.ActualWidth}, ActualHeight={VolumeProgressBar.ActualHeight}");
                    }
                    else
                    {
                        // Skip showing window due to ShowOnPrimary settings
                        Log($"Not showing window due to ShowOnPrimary setting: {Settings.Current.ShowOnPrimary}", true);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Window already visible");
                    
                    // Still ensure it's visible and on top
                    EnsureWindowVisible();
                    EnsureAlwaysOnTop();
                    
                    // Log window state
                    System.Diagnostics.Debug.WriteLine($"Existing window - IsVisible={IsVisible}, Visibility={Visibility}, WindowState={WindowState}");
                    System.Diagnostics.Debug.WriteLine($"Window dimensions - ActualWidth: {ActualWidth}, ActualHeight: {ActualHeight}");
                    System.Diagnostics.Debug.WriteLine($"Element visibility - Mute Symbol: {MuteSymbol.Visibility}, Progress Bar: {VolumeProgressBar.Visibility}, Text: {VolumePercentText.Visibility}");
                }
                
                // Log state after update
                System.Diagnostics.Debug.WriteLine($"After volume update - Volume: {volumePercent}%");
                
                // If muted (0%), check mute symbol is visible and others are hidden
                if (volumePercent == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Mute state - MuteSymbol: {MuteSymbol.Visibility}, " +
                                                     $"ProgressBar: {VolumeProgressBar.Visibility}, " +
                                                     $"VolumeText: {VolumePercentText.Visibility}");
                    
                    // Force visibility update if needed
                    if (MuteSymbol.Visibility != Visibility.Visible)
                    {
                        System.Diagnostics.Debug.WriteLine("Forcing mute symbol visibility update");
                        UpdateLayout();
                    }
                }
                else
                {
                    // Log progress bar measurements for non-zero volume
                    System.Diagnostics.Debug.WriteLine($"Volume state - ProgressBar: " +
                                                    $"ActualWidth={VolumeProgressBar.ActualWidth}, " +
                                                    $"Indicator Width={(VolumeProgressBar.ActualWidth * volumePercent / 100.0):F2}, " +
                                                    $"Visibility={VolumeProgressBar.Visibility}");
                }
                
                // Force activation to ensure visibility
                Focus();
                Activate();
                
                // Log progress bar state
                System.Diagnostics.Debug.WriteLine($"Progress bar - Visibility: {VolumeProgressBar.Visibility}, " +
                    $"IsEnabled: {VolumeProgressBar.IsEnabled}, " +
                    $"Background: {VolumeProgressBar.Background}, " +
                    $"Foreground: {VolumeProgressBar.Foreground}");
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
        
        /// <summary>
        /// Centralized method to apply text color consistently
        /// </summary>
        private void ApplyTextColor(bool forceUpdate = false)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Applying text color: {Settings.Current.TextColor}");
                
                // Create converter to convert color string to actual color
                var converter = new StringToColorConverter();
                Color textColor;
                
                try 
                {
                    // Convert string color to WPF Color
                    textColor = (Color)converter.Convert(Settings.Current.TextColor, typeof(Color), null, null);
                    System.Diagnostics.Debug.WriteLine($"Converted color: {textColor}");
                } 
                catch (Exception ex) 
                {
                    System.Diagnostics.Debug.WriteLine($"Error converting text color, using default: {ex.Message}");
                    textColor = Colors.Yellow; // Default fallback color
                }
                
                // Create a frozen brush for better thread safety and performance
                var brush = new SolidColorBrush(textColor);
                brush.Freeze();
                
                // Apply foreground brush to text element
                VolumePercentText.Foreground = brush;
                VolumePercentText.Opacity = 1.0;
                
                // Verify the brush was applied correctly
                var appliedBrush = VolumePercentText.Foreground as SolidColorBrush;
                if (appliedBrush != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Applied text color: {appliedBrush.Color}, Frozen: {appliedBrush.IsFrozen}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Applied brush is not a SolidColorBrush");
                }
                
                // If force update is requested, update the layout
                if (forceUpdate)
                {
                    VolumePercentText.UpdateLayout();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ApplyTextColor: {ex.Message}");
            }
        }
    }
}
