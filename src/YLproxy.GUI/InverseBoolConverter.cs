using System;
using System.Globalization;
using System.Windows.Data;

namespace YLproxy.GUI;

/// <summary>
/// 反转布尔值的转换器，用于按钮的 IsEnabled 属性绑定
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }
}