namespace VideoSplitter;

public static class ThemeHelper
{
    public static AppTheme GetAppTheme(bool useDarkMode) => useDarkMode ? AppTheme.Dark : AppTheme.Light;

    public static string GetCssTheme(bool useDarkMode) => useDarkMode ? "dark" : "light";

    public static void ApplyTheme(bool useDarkMode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = GetAppTheme(useDarkMode);
    }
}
