using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using YLproxy.Models;
using Color = System.Windows.Media.Color;

namespace YLproxy.GUI.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ProxyStatus status)
        {
            return status switch
            {
                ProxyStatus.Running => new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), // green
                ProxyStatus.Failed => new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20)), // red
                ProxyStatus.Stopped => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), // gray
                _ => new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string log)
        {
            var upper = log.ToUpperInvariant();
            if (upper.Contains("[ERROR]") || upper.Contains("FAIL") || upper.Contains("FATAL]"))
                return new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20)); // red
            if (upper.Contains("[WARN]"))
                return new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25)); // amber
            if (upper.Contains("成功"))
                return new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // green
            if (upper.Contains("失败") || upper.Contains(" ERROR"))
                return new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20)); // red
        }
        return new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)); // default gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
