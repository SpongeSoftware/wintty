using Wintty.Core.Abstractions;
using Wintty.Render;
using Xunit;

namespace Wintty.Tests.Render;

public class ScrollThumbTests
{
    private static ScrollThumb? ThumbFor(ScrollbarState state)
    {
        var color = new TerminalColor(0, 0, 0);
        var cells = new TerminalCell[4];
        Array.Fill(cells, new TerminalCell(string.Empty, color, color));
        var snapshot = new TerminalSnapshot(2, 2, cells, default, color, color, state);
        return RenderPlanner.Build(snapshot).ScrollThumb;
    }

    [Fact]
    public void AtTheBottom_NoThumb() => Assert.Null(ThumbFor(new ScrollbarState(100, 98, 2)));

    [Fact]
    public void WithoutHistory_NoThumb() => Assert.Null(ThumbFor(ScrollbarState.None));

    [Fact]
    public void ScrolledBack_PlacesTheThumbProportionally()
    {
        var thumb = ThumbFor(new ScrollbarState(100, 50, 25));

        Assert.NotNull(thumb);
        Assert.Equal(0.5, thumb.Value.Top, 3);
        Assert.Equal(0.25, thumb.Value.Height, 3);
    }

    [Fact]
    public void AtTheTop_ThumbStartsAtZero() => Assert.Equal(0, ThumbFor(new ScrollbarState(100, 0, 24))!.Value.Top);

    [Fact]
    public void LongHistory_KeepsAMinimumHeight()
    {
        var thumb = ThumbFor(new ScrollbarState(100_000, 10, 24))!.Value;

        Assert.Equal(0.03, thumb.Height, 3);
    }

    [Fact]
    public void ThumbNeverRunsPastTheEnd()
    {
        var thumb = ThumbFor(new ScrollbarState(100_000, 99_970, 24))!.Value;

        Assert.True(thumb.Top + thumb.Height <= 1.0000001);
    }
}
