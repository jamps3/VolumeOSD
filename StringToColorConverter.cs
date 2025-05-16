using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VolumeOSD
{
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string colorString)
                {
                    if (colorString.StartsWith("#"))
                    {
                        return (Color)ColorConverter.ConvertFromString(colorString);
                    }
                    else
                    {
                        // Handle named colors
                        var converter = new BrushConverter();
                        var brush = (SolidColorBrush)converter.ConvertFromString(colorString);
                        return brush.Color;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Color conversion error: {ex.Message}");
            }
            return Colors.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                if (color == Colors.Black) return "Black";
                if (color == Colors.White) return "White";
                if (color == Colors.Green) return "Green";
                if (color == Colors.Red) return "Red";
                if (color == Colors.Blue) return "Blue";
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return "Black";
        }
    }
}
