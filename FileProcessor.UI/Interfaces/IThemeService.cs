namespace FileProcessor.UI.Interfaces;

/// <summary>
/// Interface for theme management in the application
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Change the application theme
    /// </summary>
    /// <param name="isDark">True for dark theme, false for light theme</param>
    void ChangeTheme(bool isDark);

    /// <summary>
    /// Check if the current theme is dark
    /// </summary>
    /// <returns>True if dark theme is active</returns>
    bool IsCurrentThemeDark();
}
