namespace ACTypeBrowser.Services;

/// <summary>
/// Service for managing application theme (dark/light mode)
/// </summary>
public class ThemeService
{
    private bool _isDarkMode = true; // Default to dark mode
    
    public event Action? OnThemeChanged;

    /// <summary>
    /// Gets whether dark mode is currently active
    /// </summary>
    public bool IsDarkMode => _isDarkMode;

    /// <summary>
    /// Toggles between dark and light mode
    /// </summary>
    public void ToggleTheme()
    {
        _isDarkMode = !_isDarkMode;
        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Sets the theme explicitly
    /// </summary>
    /// <param name="isDarkMode">True for dark mode, false for light mode</param>
    public void SetTheme(bool isDarkMode)
    {
        if (_isDarkMode != isDarkMode)
        {
            _isDarkMode = isDarkMode;
            OnThemeChanged?.Invoke();
        }
    }

    /// <summary>
    /// Gets the current theme name
    /// </summary>
    public string CurrentTheme => _isDarkMode ? "dark" : "light";
}
