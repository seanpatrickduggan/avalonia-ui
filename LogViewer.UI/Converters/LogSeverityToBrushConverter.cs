using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

using FileProcessor.Core.Logging;

namespace LogViewer.UI.Converters;

public sealed class LogSeverityToBrushConverter : IValueConverter
{
    public static readonly LogSeverityToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogSeverity sev)
            return Brushes.Gray;
        return sev switch
        {
            LogSeverity.Trace => new SolidColorBrush(Color.FromArgb(0xFF, 0x77, 0x77, 0x77)),
            LogSeverity.Debug => new SolidColorBrush(Color.FromArgb(0xFF, 0x55, 0x88, 0xAA)),
            LogSeverity.Info => new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x99, 0x33)),
            LogSeverity.Warning => new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xA5, 0x00)),
            LogSeverity.Error => new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0x33, 0x33)),
            LogSeverity.Critical => new SolidColorBrush(Color.FromArgb(0xFF, 0x8B, 0x00, 0x00)),
            _ => Brushes.Gray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
