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
                    return (Color)ColorConverter.ConvertFromString(colorString);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Color conversion error: {ex.Message}");
            }
            return (Color)ColorConverter.ConvertFromString("#000000");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return "#000000";
        }
    }
}
