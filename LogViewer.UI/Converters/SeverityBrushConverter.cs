using System;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LogViewer.UI.Converters;

public sealed class SeverityBrushConverter : IValueConverter
{
    public static readonly SeverityBrushConverter Instance = new();

    private readonly IBrush _trace = Brushes.DimGray;
    private readonly IBrush _debug = Brushes.SlateGray;
    private readonly IBrush _info = Brushes.SteelBlue;
    private readonly IBrush _warn = Brushes.Goldenrod;
    private readonly IBrush _error = Brushes.IndianRed;
    private readonly IBrush _crit = Brushes.MediumVioletRed;

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var lvl = value?.ToString()?.ToLowerInvariant();
        return lvl switch
        {
            "trace" => _trace,
            "debug" => _debug,
            "information" or "info" => _info,
            "warning" or "warn" => _warn,
            "error" => _error,
            "fatal" or "critical" or "crit" => _crit,
            _ => Brushes.Gray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
