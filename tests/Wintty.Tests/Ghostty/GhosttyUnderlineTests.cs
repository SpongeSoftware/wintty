using Wintty.Core.Abstractions;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Underline styles and colours as reported by the real libghostty render state.</summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyUnderlineTests
{
    private static readonly GhosttyGateway Gateway = new();

    private static TerminalSnapshot Render(string input)
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(40, 5));
        engine.Write(System.Text.Encoding.UTF8.GetBytes(input));
        return engine.CreateSnapshot();
    }

    [Theory]
    [InlineData("\u001b[4mX", UnderlineStyle.Single)]
    [InlineData("\u001b[4:1mX", UnderlineStyle.Single)]
    [InlineData("\u001b[4:2mX", UnderlineStyle.Double)]
    [InlineData("\u001b[21mX", UnderlineStyle.Double)]
    [InlineData("\u001b[4:3mX", UnderlineStyle.Curly)]
    [InlineData("\u001b[4:4mX", UnderlineStyle.Dotted)]
    [InlineData("\u001b[4:5mX", UnderlineStyle.Dashed)]
    public void UnderlineStyles_AreReported(string input, UnderlineStyle expected) =>
        Assert.Equal(expected, Render(input)[0, 0].Underline);

    [Fact]
    public void NoUnderline_HasNoStyleAndNoColour()
    {
        var cell = Render("X")[0, 0];

        Assert.Equal(UnderlineStyle.None, cell.Underline);
        Assert.Null(cell.UnderlineColor);
    }

    [Fact]
    public void Underline_WithoutAColour_FollowsTheText()
    {
        var cell = Render("\u001b[4:3mX")[0, 0];

        Assert.Equal(UnderlineStyle.Curly, cell.Underline);
        Assert.Null(cell.UnderlineColor);
    }

    [Fact]
    public void RgbUnderlineColour_IsReported()
    {
        var cell = Render("\u001b[4:3m\u001b[58;2;255;0;128mX")[0, 0];

        Assert.Equal(new TerminalColor(255, 0, 128), cell.UnderlineColor);
    }

    [Fact]
    public void ColonSeparatedRgbUnderlineColour_IsReported()
    {
        var cell = Render("\u001b[4m\u001b[58:2::10:20:30mX")[0, 0];

        Assert.Equal(new TerminalColor(10, 20, 30), cell.UnderlineColor);
    }

    [Fact]
    public void PaletteUnderlineColour_IsResolvedToRgb()
    {
        var cell = Render("\u001b[4m\u001b[58;5;1mX")[0, 0];

        Assert.NotNull(cell.UnderlineColor);
        Assert.True(cell.UnderlineColor!.Value.Red > cell.UnderlineColor.Value.Green);
    }

    [Fact]
    public void ResettingTheUnderlineColour_ReturnsToTheTextColour()
    {
        var snapshot = Render("\u001b[4m\u001b[58;2;1;2;3mA\u001b[59mB");

        Assert.Equal(new TerminalColor(1, 2, 3), snapshot[0, 0].UnderlineColor);
        Assert.Null(snapshot[1, 0].UnderlineColor);
    }

    [Fact]
    public void UnderlineColour_IsIndependentOfTheTextColour()
    {
        var cell = Render("\u001b[31m\u001b[4m\u001b[58;2;0;255;0mX")[0, 0];

        Assert.NotEqual(cell.Foreground, cell.UnderlineColor);
        Assert.Equal(new TerminalColor(0, 255, 0), cell.UnderlineColor);
    }
}
