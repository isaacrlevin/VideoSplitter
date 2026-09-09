using VideoSplitter;

namespace VideoSplitter.Tests.Models;

public class ThemeHelperTests
{
    [Theory]
    [InlineData(true, AppTheme.Dark, "dark")]
    [InlineData(false, AppTheme.Light, "light")]
    public void ThemeMappings_ReturnExpectedValues(bool useDarkMode, AppTheme expectedAppTheme, string expectedCssTheme)
    {
        ThemeHelper.GetAppTheme(useDarkMode).Should().Be(expectedAppTheme);
        ThemeHelper.GetCssTheme(useDarkMode).Should().Be(expectedCssTheme);
    }
}
