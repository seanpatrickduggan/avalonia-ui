using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FileProcessor.UI.Converters;

public sealed class BoolToExpandGlyphConverter : IValueConverter
{
    public static readonly BoolToExpandGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var expanded = value is bool b && b;
        // Use Unicode minus (U+2212) for nicer alignment
        return expanded ? "−" : "+";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
            return s == "−"; // treat minus glyph as expanded
        return false;
    }
}
