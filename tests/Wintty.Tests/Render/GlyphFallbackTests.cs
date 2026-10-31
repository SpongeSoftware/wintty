using Wintty.Core.Abstractions;
using Wintty.Render;
using Xunit;

namespace Wintty.Tests.Render;

public class GlyphFallbackTests
{
    private static readonly TerminalColor Fg = new(200, 200, 200);
    private static readonly TerminalColor Bg = new(0, 0, 0);

    private static TerminalSnapshot Row(params string[] texts)
    {
        var cells = texts.Select(t => new TerminalCell(t, Fg, Bg)).ToArray();
        return new TerminalSnapshot(cells.Length, 1, cells, new CursorState(0, 0, false, CursorStyle.Block), Fg, Bg);
    }

    [Theory]
    [InlineData(0xE000, true)]
    [InlineData(0xE0A0, true)]
    [InlineData(0xF07B, true)]
    [InlineData(0xF8FF, true)]
    [InlineData(0xF0001, true)]
    [InlineData(0xFFFFD, true)]
    [InlineData(0x100000, true)]
    [InlineData(0x10FFFD, true)]
    [InlineData(0xDFFF, false)]
    [InlineData(0xF900, false)]
    [InlineData(0x41, false)]
    [InlineData(0x279C, false)]
    [InlineData(0x1F600, false)]
    public void IsPrivateUse_CoversThePrivateUseAreas(int codepoint, bool expected) =>
        Assert.Equal(expected, GlyphClassifier.IsPrivateUse(codepoint));

    [Fact]
    public void RoleOf_UsesTheFirstCodePointOfTheCell()
    {
        Assert.Equal(FontRole.Symbols, GlyphClassifier.RoleOf(""));
        Assert.Equal(FontRole.Symbols, GlyphClassifier.RoleOf(char.ConvertFromUtf32(0xF0001)));
        Assert.Equal(FontRole.Primary, GlyphClassifier.RoleOf("a"));
        Assert.Equal(FontRole.Primary, GlyphClassifier.RoleOf("➜"));
        Assert.Equal(FontRole.Primary, GlyphClassifier.RoleOf("\U0001F600"));
        Assert.Equal(FontRole.Primary, GlyphClassifier.RoleOf(string.Empty));
        Assert.Equal(FontRole.Primary, GlyphClassifier.RoleOf(null!));
    }

    [Fact]
    public void Planner_DrawsEachIconGlyphInItsOwnCell()
    {
        var plan = RenderPlanner.Build(Row("a", "b", "", "", "c"));

        Assert.Equal(
            [("ab", FontRole.Primary), ("", FontRole.Symbols), ("", FontRole.Symbols), ("c", FontRole.Primary)],
            plan.Texts.Select(t => (t.Text, t.Role)).ToArray());
        Assert.Equal([0, 2, 3, 4], plan.Texts.Select(t => t.Column).ToArray());
        Assert.All(plan.Texts.Where(t => t.Role == FontRole.Symbols), t => Assert.Equal(1, t.CellCount));
    }

    [Fact]
    public void Planner_PlainTextStaysPrimary()
    {
        var plan = RenderPlanner.Build(Row("h", "i"));

        Assert.All(plan.Texts, t => Assert.Equal(FontRole.Primary, t.Role));
    }

    [Fact]
    public void Resolve_PicksTheHighestPriorityInstalledFont()
    {
        var installed = new[] { "Segoe UI", "FiraCode NFM", "Symbols Nerd Font Mono" };

        Assert.Equal("Symbols Nerd Font Mono", SymbolFontResolver.Resolve(installed));
    }

    [Fact]
    public void Resolve_IsCaseInsensitiveAndReturnsTheInstalledSpelling()
    {
        Assert.Equal("firacode nfm", SymbolFontResolver.Resolve(["firacode nfm"]));
    }

    [Fact]
    public void Resolve_FallsBackToAnyMonoNerdFont()
    {
        var installed = new[] { "Zeta Nerd Font Mono", "Alpha NFM", "Arial" };

        Assert.Equal("Alpha NFM", SymbolFontResolver.Resolve(installed));
    }

    [Fact]
    public void Resolve_ReturnsNullWithoutANerdFont()
    {
        Assert.Null(SymbolFontResolver.Resolve(["Arial", "Cascadia Mono", "FiraCode NF"]));
    }

    [Fact]
    public void Resolve_HonoursACustomPreferenceList()
    {
        Assert.Equal("B", SymbolFontResolver.Resolve(["A", "B"], ["B", "A"]));
    }

    [Fact]
    public void Resolve_RejectsNull() => Assert.Throws<ArgumentNullException>(() => SymbolFontResolver.Resolve(null!));
}
