using System;
using System.Globalization;
using System.Windows.Data;

namespace VolumeOSD
{
    public class ProgressToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 4 && 
                values[0] is double value &&
                values[1] is double width &&
                values[2] is double minimum &&
                values[3] is double maximum)
            {
                if (maximum - minimum == 0) return 0.0;
                return width * (value - minimum) / (maximum - minimum);
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
