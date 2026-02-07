using System;
using System.Globalization;
using System.Windows.Data;

namespace PosApp.Desktop.Converters
{
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            if (value is bool b)
            {
                return b;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && targetType == typeof(int))
            {
                return b ? 1 : 0;
            }
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
