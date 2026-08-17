using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CoinStack.Desktop;

public class BoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is bool isTrue && parameter is string paramStr)
        {
            var parts = paramStr.Split('|');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out var trueValue) &&
                double.TryParse(parts[1], out var falseValue))
            {
                return isTrue ? trueValue : falseValue;
            }
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is bool isTrue)
        {
            return isTrue ? 1.0 : 0.0;
        }
        return 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException();
    }
}


