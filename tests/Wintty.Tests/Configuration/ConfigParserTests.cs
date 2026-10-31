using Wintty.Core.Abstractions;
using Wintty.Core.Configuration;
using Xunit;

namespace Wintty.Tests.Configuration;

public class ConfigParserTests
{
    [Fact]
    public void Parse_RejectsNull() => Assert.Throws<ArgumentNullException>(() => ConfigParser.Parse(null!));

    [Fact]
    public void Parse_EmptyTextGivesDefaults()
    {
        var result = ConfigParser.Parse(string.Empty);

        Assert.Equal(new WinttyConfig(), result.Config);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parse_ReadsEveryOption()
    {
        var result = ConfigParser.Parse(
            "font-family = JetBrains Mono\nfont-size = 13.5\nwindow-padding = 10\nscrollback-limit = 500\n" +
            "cursor-style-blink = false\ncommand = pwsh.exe -NoLogo\n");

        Assert.Empty(result.Diagnostics);
        Assert.Equal("JetBrains Mono", result.Config.FontFamily);
        Assert.Equal(13.5f, result.Config.FontSize);
        Assert.Equal(10, result.Config.WindowPadding);
        Assert.Equal(500, result.Config.ScrollbackLimit);
        Assert.False(result.Config.CursorBlink);
        Assert.Equal("pwsh.exe -NoLogo", result.Config.Command);
    }

    [Fact]
    public void Parse_IgnoresCommentsBlankLinesAndWindowsLineEndings()
    {
        var result = ConfigParser.Parse("# a comment\r\n\r\nfont-size = 12\r\n   # indented comment\r\n");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(12f, result.Config.FontSize);
    }

    [Fact]
    public void Parse_KeysAreCaseInsensitiveAndWhitespaceTolerant()
    {
        Assert.Equal(11f, ConfigParser.Parse("  FONT-SIZE=11  ").Config.FontSize);
    }

    [Fact]
    public void Parse_RemovesSurroundingQuotes()
    {
        var config = ConfigParser.Parse("font-family = \"Fira Code\"\ncommand = \"C:\\Program Files\\PowerShell\\7\\pwsh.exe\"").Config;

        Assert.Equal("Fira Code", config.FontFamily);
        Assert.Equal("C:\\Program Files\\PowerShell\\7\\pwsh.exe", config.Command);
    }

    [Fact]
    public void Parse_ValueMayContainEqualsSigns()
    {
        Assert.Equal("a=b", ConfigParser.Parse("command = a=b").Config.Command);
    }

    [Fact]
    public void Parse_LastValueOfARepeatedKeyWins()
    {
        Assert.Equal(14f, ConfigParser.Parse("font-size = 10\nfont-size = 14").Config.FontSize);
    }

    [Fact]
    public void Parse_EmptyValueResetsToTheDefault()
    {
        var config = ConfigParser.Parse(
            "font-family = X\nfont-family =\nfont-size = 20\nfont-size =\nwindow-padding = 1\nwindow-padding =\n" +
            "scrollback-limit = 5\nscrollback-limit =\n" +
            "cursor-style-blink = false\ncursor-style-blink =\ncommand = x\ncommand =").Config;

        Assert.Equal(new WinttyConfig(), config);
    }

    [Fact]
    public void Parse_BadLinesAreReportedWithLineNumbersAndValidLinesSurvive()
    {
        var result = ConfigParser.Parse(
            "font-size = 12\nnot a setting\nfont-size = huge\nbogus = 1\nfoo = nope\ncursor-style-blink = maybe\n" +
            "scrollback-limit = -1\nwindow-padding = 9999\n=value\nwindow-padding = 3");

        Assert.Equal(12f, result.Config.FontSize);
        Assert.Equal(3, result.Config.WindowPadding);
        Assert.Equal([2, 3, 4, 5, 6, 7, 8, 9], result.Diagnostics.Select(d => d.Line));
        Assert.Contains("unknown option 'bogus'", result.Diagnostics[2].Message);
        Assert.Equal("line 2: expected 'key = value' but found 'not a setting'", result.Diagnostics[0].ToString());
    }

    [Theory]
    [InlineData("nan")]
    [InlineData("3")]
    [InlineData("201")]
    public void Parse_FontSizeOutOfRangeIsRejected(string value)
    {
        var result = ConfigParser.Parse($"font-size = {value}");

        Assert.Single(result.Diagnostics);
        Assert.Equal(WinttyConfig.DefaultFontSize, result.Config.FontSize);
    }

    [Theory]
    [InlineData("#abc", 170, 187, 204)]
    [InlineData("abc", 170, 187, 204)]
    [InlineData("#0a0b0c", 10, 11, 12)]
    [InlineData("FFFFFF", 255, 255, 255)]
    public void TryParseColor_AcceptsHexForms(string text, byte red, byte green, byte blue)
    {
        Assert.True(ConfigParser.TryParseColor(text, out var color));
        Assert.Equal(new TerminalColor(red, green, blue), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#gggggg")]
    [InlineData("red")]
    public void TryParseColor_RejectsOtherText(string text) => Assert.False(ConfigParser.TryParseColor(text, out _));

    [Fact]
    public void Diagnostic_WholeFileProblemsHaveNoLinePrefix() =>
        Assert.Equal("oops", new ConfigDiagnostic(0, "oops").ToString());
}
