using Wintty.Core.Abstractions;
using Xunit;

namespace Wintty.Tests.Core;

public class ScrollbarStateTests
{
    [Fact]
    public void None_HasNoHistory()
    {
        Assert.False(ScrollbarState.None.HasHistory);
        Assert.False(ScrollbarState.None.IsScrolledBack);
    }

    [Fact]
    public void ScreenOnly_HasNoHistory() => Assert.False(new ScrollbarState(24, 0, 24).HasHistory);

    [Fact]
    public void AtTheBottom_IsNotScrolledBack()
    {
        var state = new ScrollbarState(100, 76, 24);

        Assert.True(state.HasHistory);
        Assert.False(state.IsScrolledBack);
    }

    [Fact]
    public void AboveTheBottom_IsScrolledBack() => Assert.True(new ScrollbarState(100, 10, 24).IsScrolledBack);
}
