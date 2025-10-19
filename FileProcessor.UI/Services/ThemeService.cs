using Avalonia;
using Avalonia.Styling;
using FileProcessor.UI.Interfaces;
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
                System.Diagnostics.Debug.WriteLine($"Current theme variant: {current}");
                return current == ThemeVariant.Dark;
            }
            catch
            {
                return true; // Default to dark
            }
        }
    }
}
