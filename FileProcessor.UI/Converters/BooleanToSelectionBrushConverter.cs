using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace FileProcessor.UI.Converters;

/// <summary>
/// Converts boolean selection state to appropriate background brush
/// </summary>
public class BooleanToSelectionBrushConverter : IValueConverter
{
    public static readonly BooleanToSelectionBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            // Return a light blue brush for selected items
            return new SolidColorBrush(Color.FromArgb(40, 33, 150, 243)); // Light blue with transparency
        }

        // Return transparent for unselected items
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
