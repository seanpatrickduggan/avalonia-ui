using Avalonia;
using Avalonia.Styling;
using FileProcessor.UI.Interfaces;
using Serilog;
using System;

namespace FileProcessor.UI.Services
{
    public class ThemeService : IThemeService
    {
        public void ChangeTheme(bool isDarkMode)
        {
            try
            {
                if (Application.Current != null)
                {
                    var targetTheme = isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
                    Application.Current.RequestedThemeVariant = targetTheme;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Application.Current is null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to change theme: {ex.Message}");
            }
        }

        public bool IsCurrentThemeDark()
        {
            try
            {
                var current = Application.Current?.RequestedThemeVariant;
                return current == ThemeVariant.Dark;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to check current theme, defaulting to dark");
                return true; // Default to dark
            }
        }
    }
}
