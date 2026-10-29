using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace CoinStack.Desktop;

public class BoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is not bool isTrue || parameter is not string paramStr)
            return value;

        var parts = paramStr.Split('|');
        if (parts.Length != 2)
            return value;

        if (targetType == typeof(double) || targetType == typeof(double?) ||
            (targetType == typeof(object) && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
        {
            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var trueValue) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var falseValue))
            {
                return isTrue ? trueValue : falseValue;
            }
        }

        if (Enum.TryParse(typeof(HorizontalAlignment), parts[0], ignoreCase: true, out var trueAlign) &&
            Enum.TryParse(typeof(HorizontalAlignment), parts[1], ignoreCase: true, out var falseAlign))
        {
            return isTrue ? trueAlign : falseAlign;
        }

        return isTrue ? parts[0] : parts[1];
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
            return isTrue ? 1.0 : 0.0;
        return 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Maps a bool to SuccessBrush (true) or DangerBrush (false).
/// Parameter "invert" swaps the mapping (true → Danger).
/// </summary>
public class BoolToStatusBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        var flag = value is bool b && b;
        var invert = parameter is string s &&
                     s.Equals("invert", StringComparison.OrdinalIgnoreCase);
        var useSuccess = invert ? !flag : flag;
        var key = useSuccess ? "SuccessBrush" : "DangerBrush";

        if (Application.Current is { } app &&
            app.TryGetResource(key, app.ActualThemeVariant, out var resource) &&
            resource is IBrush brush)
        {
            return brush;
        }

        return useSuccess
            ? new SolidColorBrush(Color.Parse("#16A34A"))
            : new SolidColorBrush(Color.Parse("#DC2626"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException();
    }
}
