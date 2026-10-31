using Wintty.Core.Abstractions;
using Xunit;

namespace Wintty.Tests.Core;

public class TerminalColorSchemeTests
{
    private static readonly TerminalColor Red = new(255, 0, 0);
    private static readonly TerminalColor Green = new(0, 255, 0);
    private static readonly TerminalColor Blue = new(0, 0, 255);

    [Fact]
    public void Default_ChangesNothing()
    {
        Assert.Null(TerminalColorScheme.Default.Foreground);
        Assert.Empty(TerminalColorScheme.Default.Entries);
    }

    [Fact]
    public void Overlay_TopWinsAndUnsetValuesFallThrough()
    {
        var bottom = new TerminalColorScheme(Red, Green, Blue, new Dictionary<int, TerminalColor> { [1] = Red, [2] = Red });
        var top = new TerminalColorScheme(Foreground: Blue, Palette: new Dictionary<int, TerminalColor> { [2] = Green, [3] = Green });

        var result = bottom.Overlay(top);

        Assert.Equal(Blue, result.Foreground);
        Assert.Equal(Green, result.Background);
        Assert.Equal(Blue, result.CursorColor);
        Assert.Equal(Red, result.Entries[1]);
        Assert.Equal(Green, result.Entries[2]);
        Assert.Equal(Green, result.Entries[3]);
    }

    [Fact]
    public void Overlay_RejectsNull() => Assert.Throws<ArgumentNullException>(() => TerminalColorScheme.Default.Overlay(null!));

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void Validate_RejectsOutOfRangeIndexes(int index)
    {
        var scheme = new TerminalColorScheme(Palette: new Dictionary<int, TerminalColor> { [index] = Red });

        Assert.Throws<ArgumentOutOfRangeException>(scheme.Validate);
    }

    [Fact]
    public void Validate_AcceptsTheWholeRange()
    {
        var scheme = new TerminalColorScheme(Palette: new Dictionary<int, TerminalColor> { [0] = Red, [255] = Red });

        scheme.Validate();
    }
}
