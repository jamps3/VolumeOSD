using System;
using System.Windows.Data;
using System.Globalization;

namespace VolumeOSD
{
    /// <summary>
    /// Converter for font size in "Settings saved" message and other UI elements
    /// </summary>
    public class FontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int fontSize)
            {
                return Math.Max(14, fontSize * 0.8); // Use 80% of font size but minimum 14pt
            }
            return 14; // Default
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

