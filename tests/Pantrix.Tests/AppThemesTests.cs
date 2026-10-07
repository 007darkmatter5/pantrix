using Pantrix.Themes;

namespace Pantrix.Tests;

public class AppThemesTests
{
    [Fact]
    public void Find_returns_the_theme_saved_under_that_id()
    {
        Assert.Same(AppThemes.Arcade, AppThemes.Find("arcade"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a-theme-that-was-removed")]
    public void Find_falls_back_to_the_default_when_nothing_usable_was_saved(string? saved)
    {
        Assert.Same(AppThemes.Classic, AppThemes.Find(saved));
    }

    [Fact]
    public void Ids_are_unique()
    {
        Assert.Equal(AppThemes.All.Count, AppThemes.All.Select(theme => theme.Id).Distinct().Count());
    }
}
