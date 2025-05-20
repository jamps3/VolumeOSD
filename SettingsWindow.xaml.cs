using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using System.Windows.Data;

namespace VolumeOSD
{
    public partial class SettingsWindow : Window
    {
        private MainWindow previewWindow;
        private bool isInitializing = true;

        public SettingsWindow()
        {
            InitializeComponent();
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
            Settings.Save();
            
            // Save settings
            Settings.Save();
            
            // Show the saved message with animation (handled by XAML triggers)
            SavedMessageText.Visibility = Visibility.Visible;
            SavedMessageText.Opacity = 0; // Will be animated by the storyboard
            
            // The animation will fade out automatically, but set up a timer to hide completely
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3) // Match the total animation duration
            };
            
            timer.Tick += (s, args) =>
            {
                SavedMessageText.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            
            timer.Start();
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            if (previewWindow != null)
            {
                previewWindow.Close();
                previewWindow = null;
            }

            // Create a new preview window
            previewWindow = new MainWindow();
            
            // Before showing, ensure we update any debug logs for clarity
            System.Diagnostics.Debug.WriteLine("TEST BUTTON PREVIEW: Creating preview window");
            
            // Show at 50% volume for preview
            previewWindow.UpdateVolume(50);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();

            if (previewWindow != null)
            {
                previewWindow.Close();
                previewWindow = null;
            }
        }
    }

}
