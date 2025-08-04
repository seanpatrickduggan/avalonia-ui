using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FileProcessor.UI.Converters;

public class SortIndicatorConverter : IMultiValueConverter
{
    public static readonly SortIndicatorConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || 
            values[0] is not string currentSortColumn || 
            values[1] is not bool sortAscending ||
            parameter is not string columnName)
            return "";

        if (currentSortColumn.Equals(columnName, StringComparison.OrdinalIgnoreCase))
        {
            return sortAscending ? "▲" : "▼";
        }

        return "";
    }
}
