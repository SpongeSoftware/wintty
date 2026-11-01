using Wintty.Core.Layout;
using Xunit;

namespace Wintty.Tests.Layout;

public class PaneLayoutTests
{
    private static readonly PaneRect Window = new(0, 0, 100, 60);

    private static PaneId Id(int value) => new(value);

    private static PaneNode Leaf(int value) => new PaneLeaf(Id(value));

    // 1 | (2 over 3)
    private static PaneNode ThreePanes()
    {
        var root = PaneLayout.Split(Leaf(1), Id(1), SplitDirection.Right, Id(2));
        return PaneLayout.Split(root, Id(2), SplitDirection.Down, Id(3));
    }

    [Fact]
    public void Split_ReplacesThePaneWithTwoAndKeepsTheOriginalFirst()
    {
        var root = PaneLayout.Split(Leaf(1), Id(1), SplitDirection.Right, Id(2));

        var split = Assert.IsType<PaneSplit>(root);
        Assert.Equal(SplitDirection.Right, split.Direction);
        Assert.Equal(0.5, split.Ratio);
        Assert.Equal(Leaf(1), split.First);
        Assert.Equal(Leaf(2), split.Second);
    }

    [Fact]
    public void Split_OfAnUnknownPaneChangesNothing()
    {
        var root = ThreePanes();

        Assert.Same(root, PaneLayout.Split(root, Id(99), SplitDirection.Down, Id(4)));
    }

    [Fact]
    public void Leaves_ListPanesInReadingOrder() =>
        Assert.Equal([Id(1), Id(2), Id(3)], PaneLayout.Leaves(ThreePanes()));

    [Fact]
    public void Close_GivesTheSpaceToTheSibling()
    {
        Assert.Equal(Leaf(1), PaneLayout.Close(PaneLayout.Split(Leaf(1), Id(1), SplitDirection.Right, Id(2)), Id(2)));
        Assert.Equal(Leaf(2), PaneLayout.Close(PaneLayout.Split(Leaf(1), Id(1), SplitDirection.Right, Id(2)), Id(1)));
    }

    [Fact]
    public void Close_OfTheOnlyPaneLeavesNothing() => Assert.Null(PaneLayout.Close(Leaf(1), Id(1)));

    [Fact]
    public void Close_OfAnUnknownPaneChangesNothing()
    {
        var root = ThreePanes();

        Assert.Equal(root, PaneLayout.Close(root, Id(99)));
        Assert.Equal(Leaf(1), PaneLayout.Close(Leaf(1), Id(2)));
    }

    [Fact]
    public void Arrange_DividesTheSpaceByRatio()
    {
        var rects = PaneLayout.Arrange(ThreePanes(), Window);

        Assert.Equal(new PaneRect(0, 0, 50, 60), rects[Id(1)]);
        Assert.Equal(new PaneRect(50, 0, 50, 30), rects[Id(2)]);
        Assert.Equal(new PaneRect(50, 30, 50, 30), rects[Id(3)]);
        Assert.Equal(100, rects[Id(2)].Right);
        Assert.Equal(60, rects[Id(3)].Bottom);
    }

    [Theory]
    [InlineData(1, FocusDirection.Right, 2)]
    [InlineData(2, FocusDirection.Down, 3)]
    [InlineData(3, FocusDirection.Up, 2)]
    [InlineData(2, FocusDirection.Left, 1)]
    [InlineData(3, FocusDirection.Left, 1)]
    public void Neighbour_FindsTheAdjacentPane(int from, FocusDirection direction, int expected) =>
        Assert.Equal(Id(expected), PaneLayout.Neighbour(ThreePanes(), Window, Id(from), direction));

    [Theory]
    [InlineData(1, FocusDirection.Left)]
    [InlineData(1, FocusDirection.Up)]
    [InlineData(2, FocusDirection.Up)]
    [InlineData(3, FocusDirection.Down)]
    [InlineData(3, FocusDirection.Right)]
    public void Neighbour_AtAnEdgeFindsNothing(int from, FocusDirection direction) =>
        Assert.Null(PaneLayout.Neighbour(ThreePanes(), Window, Id(from), direction));

    [Fact]
    public void Neighbour_OfAnUnknownPaneFindsNothing() =>
        Assert.Null(PaneLayout.Neighbour(ThreePanes(), Window, Id(99), FocusDirection.Left));

    [Fact]
    public void Neighbour_PrefersTheNearestOfSeveralCandidates()
    {
        // 1 | 2 | 3 side by side: from 3, left is 2 (not 1).
        var root = PaneLayout.Split(Leaf(1), Id(1), SplitDirection.Right, Id(2));
        root = PaneLayout.Split(root, Id(2), SplitDirection.Right, Id(3));

        Assert.Equal(Id(2), PaneLayout.Neighbour(root, Window, Id(3), FocusDirection.Left));
    }

    [Fact]
    public void RejectsNullTrees()
    {
        Assert.Throws<ArgumentNullException>(() => PaneLayout.Leaves(null!));
        Assert.Throws<ArgumentNullException>(() => PaneLayout.Split(null!, Id(1), SplitDirection.Right, Id(2)));
        Assert.Throws<ArgumentNullException>(() => PaneLayout.Close(null!, Id(1)));
        Assert.Throws<ArgumentNullException>(() => PaneLayout.Arrange(null!, Window));
    }
}

public class TabSetTests
{
    [Fact]
    public void ANewSet_HasNoTabs()
    {
        var set = new TabSet();

        Assert.Empty(set.Tabs);
        Assert.Null(set.Selected);
        Assert.Equal(-1, set.SelectedIndex);
    }

    [Fact]
    public void AddTab_OpensAfterTheSelectedTabAndSelectsIt()
    {
        var set = new TabSet();
        var first = set.AddTab();
        var second = set.AddTab();
        set.Select(0);

        var third = set.AddTab();

        Assert.Equal([first.Id, third.Id, second.Id], set.Tabs.Select(t => t.Id));
        Assert.Same(third, set.Selected);
        Assert.Equal(1, set.SelectedIndex);
    }

    [Fact]
    public void ManyTabs_HaveDistinctIdsAndPanes()
    {
        var set = new TabSet();
        var tabs = Enumerable.Range(0, 10).Select(_ => set.AddTab()).ToList();

        Assert.Equal(10, set.Tabs.Count);
        Assert.Equal(10, tabs.Select(t => t.Id).Distinct().Count());
        Assert.Equal(10, tabs.Select(t => t.Focused).Distinct().Count());
    }

    [Fact]
    public void CloseTab_ReturnsItsPanesSoSessionsCanBeDisposed()
    {
        var set = new TabSet();
        var tab = set.AddTab();
        var extra = set.Split(tab.Focused, SplitDirection.Right)!.Value;

        var closed = set.CloseTab(tab.Id);

        Assert.Equal(2, closed.Count);
        Assert.Contains(extra, closed);
        Assert.Empty(set.Tabs);
        Assert.Null(set.Selected);
        Assert.Empty(set.CloseTab(tab.Id));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(2, 1, 1)]
    [InlineData(0, 2, 0)]
    public void CloseTab_KeepsASensibleSelection(int selected, int closing, int expectedIndex)
    {
        var set = new TabSet();
        for (var i = 0; i < 3; i++)
        {
            set.AddTab();
        }

        set.Select(selected);
        var selectedTab = set.Selected;

        set.CloseTab(set.Tabs[closing].Id);

        Assert.Equal(expectedIndex, set.SelectedIndex);
        if (selected != closing)
        {
            Assert.Same(selectedTab, set.Selected);
        }
    }

    [Fact]
    public void Selection_MovesAndWraps()
    {
        var set = new TabSet();
        set.SelectNext();
        set.SelectPrevious();
        Assert.Equal(-1, set.SelectedIndex);

        set.AddTab();
        set.AddTab();
        set.AddTab();
        set.SelectNext();
        Assert.Equal(0, set.SelectedIndex);
        set.SelectPrevious();
        Assert.Equal(2, set.SelectedIndex);
        set.Select(1);
        Assert.Equal(1, set.SelectedIndex);
        set.Select(9);
        set.Select(-1);
        Assert.Equal(1, set.SelectedIndex);
    }

    [Fact]
    public void Move_ReordersAndKeepsTheSameTabSelected()
    {
        var set = new TabSet();
        var a = set.AddTab();
        var b = set.AddTab();
        var c = set.AddTab();
        set.Select(0);

        set.Move(0, 2);

        Assert.Equal([b.Id, c.Id, a.Id], set.Tabs.Select(t => t.Id));
        Assert.Same(a, set.Selected);
        set.Move(0, 0);
        set.Move(-1, 1);
        set.Move(0, 9);
        Assert.Equal([b.Id, c.Id, a.Id], set.Tabs.Select(t => t.Id));
    }

    [Fact]
    public void Titles_PreferTheCustomNameThenTheShellTitle()
    {
        var set = new TabSet();
        var tab = set.AddTab();
        Assert.Equal("wintty", tab.Title);

        set.SetShellTitle(tab.Focused, "pwsh");
        Assert.Equal("pwsh", tab.Title);

        set.Rename(tab.Id, "  build  ");
        Assert.Equal("build", tab.Title);

        set.Rename(tab.Id, " ");
        Assert.Equal("pwsh", tab.Title);
        set.Rename(999, "ignored");
    }

    [Fact]
    public void OnlyTheFocusedPaneNamesTheTab()
    {
        var set = new TabSet();
        var tab = set.AddTab();
        var first = tab.Focused;
        var second = set.Split(first, SplitDirection.Right)!.Value;

        set.SetShellTitle(first, "background");
        set.SetShellTitle(second, "front");

        Assert.Equal("front", tab.Title);
        set.SetShellTitle(new PaneId(999), "nobody");
    }

    [Fact]
    public void Split_FocusesTheNewPane()
    {
        var set = new TabSet();
        var tab = set.AddTab();
        var original = tab.Focused;

        var created = set.Split(original, SplitDirection.Down)!.Value;

        Assert.Equal(created, tab.Focused);
        Assert.Equal([original, created], PaneLayout.Leaves(tab.Root));
        Assert.Same(tab, set.TabOf(created));
        Assert.Null(set.Split(new PaneId(999), SplitDirection.Right));
        Assert.Null(set.TabOf(new PaneId(999)));
    }

    [Fact]
    public void ClosePane_MovesFocusAndClosesTheTabWithItsLastPane()
    {
        var set = new TabSet();
        var tab = set.AddTab();
        var first = tab.Focused;
        var second = set.Split(first, SplitDirection.Right)!.Value;

        Assert.False(set.ClosePane(second));
        Assert.Equal(first, tab.Focused);

        Assert.True(set.ClosePane(first));
        Assert.Empty(set.Tabs);
        Assert.False(set.ClosePane(first));
    }

    [Fact]
    public void ClosingAnUnfocusedPane_KeepsFocusWhereItWas()
    {
        var set = new TabSet();
        var tab = set.AddTab();
        var first = tab.Focused;
        var second = set.Split(first, SplitDirection.Right)!.Value;

        set.ClosePane(first);

        Assert.Equal(second, tab.Focused);
    }

    [Fact]
    public void FocusNeighbour_MovesAcrossSplitsAndStopsAtEdges()
    {
        var set = new TabSet();
        var tab = set.AddTab();
        var left = tab.Focused;
        var right = set.Split(left, SplitDirection.Right)!.Value;
        var bounds = new PaneRect(0, 0, 100, 60);

        Assert.Equal(left, set.FocusNeighbour(FocusDirection.Left, bounds));
        Assert.Null(set.FocusNeighbour(FocusDirection.Left, bounds));
        Assert.Equal(right, set.FocusNeighbour(FocusDirection.Right, bounds));
        Assert.Equal(right, tab.Focused);
    }

    [Fact]
    public void FocusNeighbour_WithNoTabsDoesNothing() =>
        Assert.Null(new TabSet().FocusNeighbour(FocusDirection.Left, new PaneRect(0, 0, 1, 1)));

    [Fact]
    public void Zoom_NeedsSeveralPanesAndFollowsFocusAndEnds()
    {
        var set = new TabSet();
        new TabSet().ToggleZoom();
        var tab = set.AddTab();
        set.ToggleZoom();
        Assert.Null(tab.Zoomed);

        var first = tab.Focused;
        var second = set.Split(first, SplitDirection.Right)!.Value;
        set.ToggleZoom();
        Assert.Equal(second, tab.Zoomed);
        Assert.Null(set.FocusNeighbour(FocusDirection.Left, new PaneRect(0, 0, 100, 60)));

        set.Focus(first);
        Assert.Equal(first, tab.Zoomed);

        set.ToggleZoom();
        Assert.Null(tab.Zoomed);

        set.ToggleZoom();
        set.ClosePane(first);
        Assert.Null(tab.Zoomed);

        set.ToggleZoom();
        set.Focus(new PaneId(999));
        var third = set.Split(second, SplitDirection.Down)!.Value;
        Assert.Null(tab.Zoomed);
        Assert.Equal(third, tab.Focused);
    }
}
