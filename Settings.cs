using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VolumeOSD
{
    public class Settings : INotifyPropertyChanged
    {
        public static Settings Current { get; set; } = new Settings();

        private bool startHidden = false;
        private bool startWithWindows = false;
        private int fontSize = 20;
        private string textColor = "White";
        private string backgroundColor = "Black";
        private string progressBarColor = "#00FF00";  // Default lime green
        private int transparency = 80;
        private string position = "Bottom Right";
        private bool showOnPrimary = false;
        private bool showTrayIcon = true;
        private int displayDuration = 3; // seconds

        public event PropertyChangedEventHandler PropertyChanged;

        public bool StartHidden
        {
            get => startHidden;
            set => SetProperty(ref startHidden, value);
        }

        public bool StartWithWindows
        {
            get => startWithWindows;
            set => SetProperty(ref startWithWindows, value);
        }

        public int FontSize
        {
            get => fontSize;
            set => SetProperty(ref fontSize, value);
        }

        public string TextColor
        {
            get => textColor;
            set => SetProperty(ref textColor, value);
        }

        public string BackgroundColor
        {
            get => backgroundColor;
            set => SetProperty(ref backgroundColor, value);
        }

        public string ProgressBarColor
        {
            get => progressBarColor;
            set => SetProperty(ref progressBarColor, value);
        }

        public int Transparency
        {
            get => transparency;
            set
            {
                if (SetProperty(ref transparency, value))
                {
                    OnPropertyChanged(nameof(BackgroundOpacity));
                    OnPropertyChanged(nameof(WindowOpacity));
                }
            }
        }

        // Helper property for binding - not serialized
        [JsonIgnore]
        public double BackgroundOpacity
        {
            get => (100 - Transparency) / 100.0;
        }

        [JsonIgnore]
        public double WindowOpacity
        {
            get => (100 - Transparency) / 100.0;
        }

        public string Position
        {
            get => position;
            set
            {
                if (value != null && value != position)
                {
                    System.Diagnostics.Debug.WriteLine($"Position changing from {position} to {value}");
                    position = value;
                    OnPropertyChanged(nameof(Position));
                    Save();
                }
            }
        }

        public bool ShowOnPrimary
        {
            get => showOnPrimary;
            set
            {
                if (SetProperty(ref showOnPrimary, value))
                {
                    OnPropertyChanged(nameof(Position));
                }
            }
        }

        public bool ShowTrayIcon
        {
            get => showTrayIcon;
            set => SetProperty(ref showTrayIcon, value);
        }

        public int DisplayDuration
        {
            get => displayDuration;
            set => SetProperty(ref displayDuration, value);
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            Save();
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            System.Diagnostics.Debug.WriteLine($"Property changed: {propertyName}");
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static readonly string FilePath = "settings.json";

        public static void Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    var loadedSettings = JsonSerializer.Deserialize<Settings>(json);
                    if (loadedSettings != null)
                    {
                        Current = loadedSettings;
                        System.Diagnostics.Debug.WriteLine($"Settings loaded. Position: {Current.Position}, FontSize: {Current.FontSize}");
                    }
                    else
                    {
                        Current = new Settings();
                    }
                    
                    // Clean up the settings file to remove any computed properties
                    Save();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                    Current = new Settings();
                }
            }
            else
            {
                Current = new Settings();
                Save();
            }
        }

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(FilePath, json);
                System.Diagnostics.Debug.WriteLine($"Settings saved. Position: {Current.Position}, FontSize: {Current.FontSize}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
