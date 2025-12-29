using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using InternetMonitor.Core.Models;

namespace InternetMonitor.UI.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionState state)
        {
            return state switch
            {
                ConnectionState.Connected => new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                ConnectionState.Unstable => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7)),
                ConnectionState.Disconnected => new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)),
                _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158))
            };
        }

        if (value is bool isConnected)
        {
            return isConnected
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));
        }

        return new SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // "Inverse" parametresi varsa sonucu tersine çevir
            if (parameter?.ToString() == "Inverse")
            {
                boolValue = !boolValue;
            }
            return boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}
