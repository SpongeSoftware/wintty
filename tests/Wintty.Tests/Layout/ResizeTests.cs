using Wintty.Core.Layout;
using Xunit;

namespace Wintty.Tests.Layout;

public class ResizeTests
{
    private static PaneId Id(int value) => new(value);

    private static PaneNode Leaf(int value) => new PaneLeaf(Id(value));

    // 1 | (2 over 3)
    private static PaneNode ThreePanes()
    {
        var root = PaneLayout.Split(Leaf(1), Id(1), SplitDirection.Right, Id(2));
        return PaneLayout.Split(root, Id(2), SplitDirection.Down, Id(3));
    }

    private static double RootRatio(PaneNode root) => ((PaneSplit)root).Ratio;

    private static double InnerRatio(PaneNode root) => (double)((PaneSplit)((PaneSplit)root).Second).Ratio;

    [Fact]
    public void Resize_MovesTheDividerInTheRequestedDirection()
    {
        var wider = PaneLayout.Resize(ThreePanes(), Id(1), FocusDirection.Right, 0.1);
        var narrower = PaneLayout.Resize(ThreePanes(), Id(1), FocusDirection.Left, 0.1);

        Assert.Equal(0.6, RootRatio(wider), 6);
        Assert.Equal(0.4, RootRatio(narrower), 6);
    }

    [Fact]
    public void Resize_UsesTheNearestDividerOnTheMatchingAxis()
    {
        var down = PaneLayout.Resize(ThreePanes(), Id(3), FocusDirection.Down, 0.2);
        var sideways = PaneLayout.Resize(ThreePanes(), Id(3), FocusDirection.Right, 0.2);

        Assert.Equal(0.7, InnerRatio(down), 6);
        Assert.Equal(0.5, RootRatio(down), 6);
        Assert.Equal(0.7, RootRatio(sideways), 6);
        Assert.Equal(0.5, InnerRatio(sideways), 6);
    }

    [Fact]
    public void Resize_NeverShrinksASideBelowTheMinimum()
    {
        var root = ThreePanes();
        for (var i = 0; i < 30; i++)
        {
            root = PaneLayout.Resize(root, Id(1), FocusDirection.Left, 0.1);
        }

        Assert.Equal(PaneLayout.MinimumRatio, RootRatio(root), 6);

        for (var i = 0; i < 30; i++)
        {
            root = PaneLayout.Resize(root, Id(1), FocusDirection.Right, 0.1);
        }

        Assert.Equal(1 - PaneLayout.MinimumRatio, RootRatio(root), 6);
    }

    [Fact]
    public void Resize_WithNoDividerOnThatAxis_ChangesNothing()
    {
        var single = Leaf(1);
        Assert.Same(single, PaneLayout.Resize(single, Id(1), FocusDirection.Right, 0.1));
        var vertical = PaneLayout.Split(Leaf(1), Id(1), SplitDirection.Down, Id(2));

        Assert.Equal(vertical, PaneLayout.Resize(vertical, Id(1), FocusDirection.Right, 0.1));
        Assert.Equal(ThreePanes(), PaneLayout.Resize(ThreePanes(), Id(99), FocusDirection.Right, 0.1));
    }

    [Fact]
    public void Equalize_SetsEverySplitToHalf()
    {
        var root = PaneLayout.Resize(PaneLayout.Resize(ThreePanes(), Id(1), FocusDirection.Right, 0.2), Id(3), FocusDirection.Down, 0.2);

        var equal = PaneLayout.Equalize(root);

        Assert.Equal(0.5, RootRatio(equal));
        Assert.Equal(0.5, InnerRatio(equal));
        Assert.Equal(Leaf(1), PaneLayout.Equalize(Leaf(1)));
    }

    [Fact]
    public void ResizeAndEqualize_RejectNullTrees()
    {
        Assert.Throws<ArgumentNullException>(() => PaneLayout.Resize(null!, Id(1), FocusDirection.Left, 0.1));
        Assert.Throws<ArgumentNullException>(() => PaneLayout.Equalize(null!));
    }

    [Fact]
    public void TabSet_ResizesTheFocusedPaneOfTheSelectedTab()
    {
        var set = new TabSet();
        Assert.False(set.ResizeFocused(FocusDirection.Right));
        set.EqualizeSplits();

        var tab = set.AddTab();
        var first = tab.Focused;
        Assert.False(set.ResizeFocused(FocusDirection.Right));

        set.Split(first, SplitDirection.Right);
        set.Focus(first);
        Assert.True(set.ResizeFocused(FocusDirection.Right));
        Assert.Equal(0.55, RootRatio(tab.Root), 6);

        set.EqualizeSplits();
        Assert.Equal(0.5, RootRatio(tab.Root));
    }

    [Fact]
    public void TabSet_DoesNotResizeWhileZoomed()
    {
        var set = new TabSet();
        var tab = set.AddTab();
        set.Split(tab.Focused, SplitDirection.Right);
        set.ToggleZoom();

        Assert.False(set.ResizeFocused(FocusDirection.Left));
    }
}

public class FontSizingTests
{
    [Fact]
    public void Zoom_MovesBySteps() => Assert.Equal(17f, Wintty.Core.Configuration.FontSizing.Zoom(15f, 2));

    [Fact]
    public void Zoom_StaysWithinTheConfigurableRange()
    {
        Assert.Equal(Wintty.Core.Configuration.FontSizing.Minimum, Wintty.Core.Configuration.FontSizing.Zoom(5f, -10));
        Assert.Equal(Wintty.Core.Configuration.FontSizing.Maximum, Wintty.Core.Configuration.FontSizing.Zoom(199f, 10));
    }
}
