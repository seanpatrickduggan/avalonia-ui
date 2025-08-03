
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileProcessor.UI.Services;

namespace FileProcessor.UI.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly ThemeService _themeService;
        private bool _isDarkMode;
        private bool _isUpdatingTheme = false;

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (!_isUpdatingTheme && SetProperty(ref _isDarkMode, value))
                {
                    _isUpdatingTheme = true;
                    _themeService.ChangeTheme(value);
                    _isUpdatingTheme = false;
                }
            }
        }

        public SettingsViewModel()
        {
            _themeService = new ThemeService();
            
            // Get current theme from ThemeService
            _isDarkMode = _themeService.IsCurrentThemeDark();
        }
    }
}
