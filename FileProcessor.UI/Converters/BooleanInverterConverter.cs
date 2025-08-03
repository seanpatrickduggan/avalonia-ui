using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FileProcessor.UI.Converters;

/// <summary>
/// Converts boolean values to opposite boolean values (useful for negating bindings)
/// </summary>
public class BooleanInverterConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }
}
