using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FileProcessor.UI.Converters;

public class WorkspaceActiveConverter : IValueConverter
{
    public static readonly WorkspaceActiveConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string workspacePath && parameter is string activeWorkspace)
        {
            return workspacePath.Equals(activeWorkspace, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
