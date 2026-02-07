using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PosApp.Desktop.Views
{
    public class BoolToLockIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLocked)
            {
                return isLocked ? "\uE72E" : "\uE785"; // Locked: E72E, Unlocked: E785
            }
            return "\uE785";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLocked)
            {
                return isLocked ? new SolidColorBrush(Color.FromRgb(255, 140, 0)) : new SolidColorBrush(Color.FromRgb(100, 100, 100)); // Orange when locked, gray when unlocked
            }
            return new SolidColorBrush(Color.FromRgb(100, 100, 100));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
