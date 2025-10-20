using System.Globalization;
using FileProcessor.UI.Converters;
using FileProcessor.Core.Logging;
using FluentAssertions;
using Xunit;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace FileProcessor.UI.Tests.Converters;

public class ConvertersTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BooleanInverterConverter_Convert_InvertsBool(bool input, bool expected)
    {
        var converter = new BooleanInverterConverter();
        var result = converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void BooleanInverterConverter_Convert_NonBool_ReturnsValue()
    {
        var converter = new BooleanInverterConverter();
        var input = "string";
        var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
        result.Should().Be(input);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void BooleanInverterConverter_ConvertBack_InvertsBool(bool input, bool expected)
    {
        var converter = new BooleanInverterConverter();
        var result = converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void BooleanInverterConverter_ConvertBack_NonBool_ReturnsValue()
    {
        var converter = new BooleanInverterConverter();
        var input = 123;
        var result = converter.ConvertBack(input, typeof(int), null, CultureInfo.InvariantCulture);
        result.Should().Be(input);
    }

    [Theory]
    [InlineData(true, "−")]
    [InlineData(false, "+")]
    public void BoolToExpandGlyphConverter_Convert_ReturnsCorrectGlyph(bool input, string expected)
    {
        var converter = BoolToExpandGlyphConverter.Instance;
        var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void BoolToExpandGlyphConverter_Convert_NonBool_ReturnsPlus()
    {
        var converter = BoolToExpandGlyphConverter.Instance;
        var result = converter.Convert("string", typeof(string), null, CultureInfo.InvariantCulture);
        result.Should().Be("+");
    }

    [Theory]
    [InlineData("−", true)]
    [InlineData("+", false)]
    [InlineData("other", false)]
    public void BoolToExpandGlyphConverter_ConvertBack_ReturnsBool(string input, bool expected)
    {
        var converter = BoolToExpandGlyphConverter.Instance;
        var result = converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void BoolToExpandGlyphConverter_ConvertBack_NonString_ReturnsFalse()
    {
        var converter = BoolToExpandGlyphConverter.Instance;
        var result = converter.ConvertBack(123, typeof(bool), null, CultureInfo.InvariantCulture);
        result.Should().Be(false);
    }

    // Add tests for other converters similarly
    // For brevity, I'll add basic tests; you can expand as needed

    [AvaloniaFact]
    public void BooleanToSelectionBrushConverter_Convert_ReturnsBrush()
    {
        var converter = new BooleanToSelectionBrushConverter();
        var result = converter.Convert(true, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void BooleanToSelectionBrushConverter_Convert_True_ReturnsLightBlueBrush()
    {
        var converter = BooleanToSelectionBrushConverter.Instance;
        var result = converter.Convert(true, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().BeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result!;
        brush.Color.Should().Be(Color.FromArgb(40, 33, 150, 243));
    }

    [AvaloniaFact]
    public void BooleanToSelectionBrushConverter_Convert_False_ReturnsTransparentBrush()
    {
        var converter = BooleanToSelectionBrushConverter.Instance;
        var result = converter.Convert(false, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Transparent);
    }

    [AvaloniaFact]
    public void BooleanToSelectionBrushConverter_Convert_NonBool_ReturnsTransparentBrush()
    {
        var converter = BooleanToSelectionBrushConverter.Instance;
        var result = converter.Convert("string", typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Transparent);
    }

    [AvaloniaFact]
    public void BooleanToSelectionBrushConverter_Convert_Null_ReturnsTransparentBrush()
    {
        var converter = BooleanToSelectionBrushConverter.Instance;
        var result = converter.Convert(null, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Transparent);
    }

    [Fact]
    public void BooleanToSelectionBrushConverter_ConvertBack_ThrowsNotImplementedException()
    {
        var converter = BooleanToSelectionBrushConverter.Instance;
        Action act = () => converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture);
        act.Should().Throw<NotImplementedException>();
    }

    [AvaloniaFact]
    public void LogSeverityToBrushConverter_Convert_ReturnsBrush()
    {
        var converter = new LogSeverityToBrushConverter();
        var result = converter.Convert(0, typeof(object), null, CultureInfo.InvariantCulture); // Assuming LogSeverity enum
        result.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void LogSeverityToBrushConverter_Convert_Trace_ReturnsGrayBrush()
    {
        var converter = LogSeverityToBrushConverter.Instance;
        var result = converter.Convert(LogSeverity.Trace, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().BeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result!;
        brush.Color.Should().Be(Color.FromArgb(0xFF, 0x77, 0x77, 0x77));
    }

    [AvaloniaFact]
    public void LogSeverityToBrushConverter_Convert_Debug_ReturnsBlueBrush()
    {
        var converter = LogSeverityToBrushConverter.Instance;
        var result = converter.Convert(LogSeverity.Debug, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().BeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result!;
        brush.Color.Should().Be(Color.FromArgb(0xFF, 0x55, 0x88, 0xAA));
    }

    [AvaloniaFact]
    public void LogSeverityToBrushConverter_Convert_Info_ReturnsGreenBrush()
    {
        var converter = LogSeverityToBrushConverter.Instance;
        var result = converter.Convert(LogSeverity.Info, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().BeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result!;
        brush.Color.Should().Be(Color.FromArgb(0xFF, 0x33, 0x99, 0x33));
    }

    [AvaloniaFact]
    public void LogSeverityToBrushConverter_Convert_Warning_ReturnsOrangeBrush()
    {
        var converter = LogSeverityToBrushConverter.Instance;
        var result = converter.Convert(LogSeverity.Warning, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().BeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result!;
        brush.Color.Should().Be(Color.FromArgb(0xFF, 0xFF, 0xA5, 0x00));
    }

    [AvaloniaFact]
    public void LogSeverityToBrushConverter_Convert_Error_ReturnsRedBrush()
    {
        var converter = LogSeverityToBrushConverter.Instance;
        var result = converter.Convert(LogSeverity.Error, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().BeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result!;
        brush.Color.Should().Be(Color.FromArgb(0xFF, 0xE5, 0x33, 0x33));
    }

    [AvaloniaFact]
    public void LogSeverityToBrushConverter_Convert_Critical_ReturnsDarkRedBrush()
    {
        var converter = LogSeverityToBrushConverter.Instance;
        var result = converter.Convert(LogSeverity.Critical, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().BeOfType<SolidColorBrush>();
        var brush = (SolidColorBrush)result!;
        brush.Color.Should().Be(Color.FromArgb(0xFF, 0x8B, 0x00, 0x00));
    }

    [AvaloniaFact]
    public void LogSeverityToBrushConverter_Convert_InvalidValue_ReturnsGrayBrush()
    {
        var converter = LogSeverityToBrushConverter.Instance;
        var result = converter.Convert("invalid", typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Gray);
    }

    [AvaloniaFact]
    public void LogSeverityToBrushConverter_Convert_InvalidEnumValue_ReturnsGrayBrush()
    {
        var converter = LogSeverityToBrushConverter.Instance;
        // Cast an invalid int to LogSeverity to hit the default case
        var invalidSeverity = (LogSeverity)999;
        var result = converter.Convert(invalidSeverity, typeof(object), null, CultureInfo.InvariantCulture);
        result.Should().Be(Brushes.Gray);
    }

    [Fact]
    public void LogSeverityToBrushConverter_ConvertBack_ThrowsNotSupportedException()
    {
        var converter = LogSeverityToBrushConverter.Instance;
        Action act = () => converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SortIndicatorConverter_Convert_ReturnsString()
    {
        var converter = SortIndicatorConverter.Instance;
        var values = new List<object?> { "column1", true, "column1" };
        var result = converter.Convert(values, typeof(string), "column1", CultureInfo.InvariantCulture);
        result.Should().Be("▲");
    }

    [Fact]
    public void SortIndicatorConverter_Convert_InsufficientValues_ReturnsEmptyString()
    {
        var converter = SortIndicatorConverter.Instance;
        var values = new List<object?> { "column1" }; // Only 1 value
        var result = converter.Convert(values, typeof(string), "column1", CultureInfo.InvariantCulture);
        result.Should().Be("");
    }

    [Fact]
    public void SortIndicatorConverter_Convert_FirstValueNotString_ReturnsEmptyString()
    {
        var converter = SortIndicatorConverter.Instance;
        var values = new List<object?> { 123, true, "column1" }; // First value is int
        var result = converter.Convert(values, typeof(string), "column1", CultureInfo.InvariantCulture);
        result.Should().Be("");
    }

    [Fact]
    public void SortIndicatorConverter_Convert_SecondValueNotBool_ReturnsEmptyString()
    {
        var converter = SortIndicatorConverter.Instance;
        var values = new List<object?> { "column1", "notbool", "column1" }; // Second value is string
        var result = converter.Convert(values, typeof(string), "column1", CultureInfo.InvariantCulture);
        result.Should().Be("");
    }

    [Fact]
    public void SortIndicatorConverter_Convert_ParameterNotString_ReturnsEmptyString()
    {
        var converter = SortIndicatorConverter.Instance;
        var values = new List<object?> { "column1", true, "column1" };
        var result = converter.Convert(values, typeof(string), 123, CultureInfo.InvariantCulture); // Parameter is int
        result.Should().Be("");
    }

    [Fact]
    public void SortIndicatorConverter_Convert_MatchingColumn_Ascending_ReturnsUpArrow()
    {
        var converter = SortIndicatorConverter.Instance;
        var values = new List<object?> { "column1", true, "column1" };
        var result = converter.Convert(values, typeof(string), "column1", CultureInfo.InvariantCulture);
        result.Should().Be("▲");
    }

    [Fact]
    public void SortIndicatorConverter_Convert_MatchingColumn_Descending_ReturnsDownArrow()
    {
        var converter = SortIndicatorConverter.Instance;
        var values = new List<object?> { "column1", false, "column1" };
        var result = converter.Convert(values, typeof(string), "column1", CultureInfo.InvariantCulture);
        result.Should().Be("▼");
    }

    [Fact]
    public void SortIndicatorConverter_Convert_NonMatchingColumn_ReturnsEmptyString()
    {
        var converter = SortIndicatorConverter.Instance;
        var values = new List<object?> { "column1", true, "column1" };
        var result = converter.Convert(values, typeof(string), "column2", CultureInfo.InvariantCulture);
        result.Should().Be("");
    }

    [Fact]
    public void SortIndicatorConverter_Convert_CaseInsensitiveMatch_ReturnsArrow()
    {
        var converter = SortIndicatorConverter.Instance;
        var values = new List<object?> { "COLUMN1", true, "column1" };
        var result = converter.Convert(values, typeof(string), "column1", CultureInfo.InvariantCulture);
        result.Should().Be("▲");
    }

    [Fact]
    public void WorkspaceActiveConverter_Convert_ReturnsBool()
    {
        var converter = WorkspaceActiveConverter.Instance;
        var result = converter.Convert("path1", typeof(bool), "path1", CultureInfo.InvariantCulture);
        result.Should().Be(true);
    }

    [Fact]
    public void WorkspaceActiveConverter_Convert_MatchingPaths_ReturnsTrue()
    {
        var converter = WorkspaceActiveConverter.Instance;
        var result = converter.Convert("path1", typeof(bool), "path1", CultureInfo.InvariantCulture);
        result.Should().Be(true);
    }

    [Fact]
    public void WorkspaceActiveConverter_Convert_NonMatchingPaths_ReturnsFalse()
    {
        var converter = WorkspaceActiveConverter.Instance;
        var result = converter.Convert("path1", typeof(bool), "path2", CultureInfo.InvariantCulture);
        result.Should().Be(false);
    }

    [Fact]
    public void WorkspaceActiveConverter_Convert_CaseInsensitiveMatch_ReturnsTrue()
    {
        var converter = WorkspaceActiveConverter.Instance;
        var result = converter.Convert("PATH1", typeof(bool), "path1", CultureInfo.InvariantCulture);
        result.Should().Be(true);
    }

    [Fact]
    public void WorkspaceActiveConverter_Convert_ValueNotString_ReturnsFalse()
    {
        var converter = WorkspaceActiveConverter.Instance;
        var result = converter.Convert(123, typeof(bool), "path1", CultureInfo.InvariantCulture);
        result.Should().Be(false);
    }

    [Fact]
    public void WorkspaceActiveConverter_Convert_ParameterNotString_ReturnsFalse()
    {
        var converter = WorkspaceActiveConverter.Instance;
        var result = converter.Convert("path1", typeof(bool), 123, CultureInfo.InvariantCulture);
        result.Should().Be(false);
    }

    [Fact]
    public void WorkspaceActiveConverter_Convert_BothNotString_ReturnsFalse()
    {
        var converter = WorkspaceActiveConverter.Instance;
        var result = converter.Convert(123, typeof(bool), 456, CultureInfo.InvariantCulture);
        result.Should().Be(false);
    }

    [Fact]
    public void WorkspaceActiveConverter_ConvertBack_ThrowsNotImplementedException()
    {
        var converter = WorkspaceActiveConverter.Instance;
        Action act = () => converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture);
        act.Should().Throw<NotImplementedException>();
    }
}
