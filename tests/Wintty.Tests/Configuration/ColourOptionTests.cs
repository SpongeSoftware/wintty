using Wintty.Core.Abstractions;
using Wintty.Core.Configuration;
using Xunit;

namespace Wintty.Tests.Configuration;

public sealed class ColourOptionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wintty-colours-" + Guid.NewGuid().ToString("N"));

    public ColourOptionTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Parse_ReadsColourOptions()
    {
        var config = ConfigParser.Parse("foreground = #fff\nbackground = 101010\ncursor-color = #ff0000\ntheme = Nord").Config;

        Assert.Equal(new TerminalColor(255, 255, 255), config.Foreground);
        Assert.Equal(new TerminalColor(16, 16, 16), config.Background);
        Assert.Equal(new TerminalColor(255, 0, 0), config.CursorColor);
        Assert.Equal("Nord", config.Theme);
        Assert.Equal(4, config.ThemeLine);
    }

    [Fact]
    public void Parse_PaletteEntriesAccumulateAndALaterEntryReplacesTheSameIndex()
    {
        var config = ConfigParser.Parse("palette = 0=#000000\npalette = 255=#ffffff\npalette = 0=#010101").Config;

        Assert.Equal(2, config.Palette.Count);
        Assert.Equal(new TerminalColor(1, 1, 1), config.Palette[0]);
        Assert.Equal(new TerminalColor(255, 255, 255), config.Palette[255]);
    }

    [Fact]
    public void Parse_EmptyValuesResetColoursPaletteAndTheme()
    {
        var config = ConfigParser.Parse(
            "foreground = #fff\nforeground =\nbackground = #000\nbackground =\ncursor-color = #123\ncursor-color =\n" +
            "palette = 1=#123456\npalette =\ntheme = Nord\ntheme =").Config;

        Assert.Equal(new WinttyConfig(), config);
    }

    [Theory]
    [InlineData("palette = 256=#000000")]
    [InlineData("palette = -1=#000000")]
    [InlineData("palette = x=#000000")]
    [InlineData("palette = 1=blue")]
    [InlineData("palette = 1")]
    [InlineData("palette = =#000000")]
    [InlineData("foreground = cell-foreground")]
    [InlineData("cursor-color = nope")]
    public void Parse_BadColourLinesAreReported(string line)
    {
        var result = ConfigParser.Parse(line);

        Assert.Single(result.Diagnostics);
        Assert.Equal(new WinttyConfig(), result.Config);
    }

    [Fact]
    public void Parse_ColoursPropertyHoldsOnlyTheFileOwnColours()
    {
        var result = ConfigParser.Parse("theme = Nord\nbackground = #010203");

        Assert.Equal(new TerminalColor(1, 2, 3), result.Colors.Background);
        Assert.Null(result.Colors.Foreground);
    }

    [Fact]
    public void ParseTheme_RejectsNull() => Assert.Throws<ArgumentNullException>(() => ConfigParser.ParseTheme(null!));

    [Fact]
    public void ParseTheme_IgnoresEverythingExceptColours()
    {
        var scheme = ConfigParser.ParseTheme("cursor-text = #000000\nselection-background = #ffffff\nbackground = oops\nfoo\nforeground = #0a0a0a");

        Assert.Equal(new TerminalColor(10, 10, 10), scheme.Foreground);
        Assert.Null(scheme.Background);
    }

    [Fact]
    public void Load_ResolvesThemeFromTheUserFolderAndSortsDiagnostics()
    {
        var themes = Path.Combine(_directory, "themes");
        Directory.CreateDirectory(themes);
        File.WriteAllText(Path.Combine(themes, "mine"), "background = #0a0b0c");
        var path = Path.Combine(_directory, "config");
        File.WriteAllText(path, "theme = mine\nbogus = 1");

        var result = ConfigLoader.Load(path, themes);

        Assert.Equal(new TerminalColor(10, 11, 12), result.Colors.Background);
        Assert.Equal(2, Assert.Single(result.Diagnostics).Line);
    }

    [Fact]
    public void Load_UnknownThemeIsADiagnosticAndKeepsTheOtherColours()
    {
        var path = Path.Combine(_directory, "config");
        File.WriteAllText(path, "bogus = 1\ntheme = missing\nforeground = #010101");

        var result = ConfigLoader.Load(path);

        Assert.Equal([1, 2], result.Diagnostics.Select(d => d.Line));
        Assert.Equal(new TerminalColor(1, 1, 1), result.Colors.Foreground);
    }
}
