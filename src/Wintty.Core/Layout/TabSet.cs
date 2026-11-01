namespace Wintty.Core.Layout;

/// <summary>One tab: a tree of panes, which pane has focus, and a title.</summary>
public sealed class Tab
{
    internal Tab(int id, PaneId firstPane)
    {
        Id = id;
        Root = new PaneLeaf(firstPane);
        Focused = firstPane;
    }

    /// <summary>Identifies the tab for as long as the window lives, however tabs are reordered.</summary>
    public int Id { get; }

    /// <summary>How the tab's panes are arranged.</summary>
    public PaneNode Root { get; internal set; }

    /// <summary>The pane that receives keyboard input.</summary>
    public PaneId Focused { get; internal set; }

    /// <summary>The pane shown alone, filling the tab, or <see langword="null"/> when all panes are shown.</summary>
    public PaneId? Zoomed { get; internal set; }

    /// <summary>A name the user chose; wins over <see cref="ShellTitle"/> when set.</summary>
    public string? CustomTitle { get; internal set; }

    /// <summary>The title the focused program asked for.</summary>
    public string? ShellTitle { get; internal set; }

    /// <summary>What the tab strip shows: the custom title, else the shell title, else a default.</summary>
    public string Title => !string.IsNullOrWhiteSpace(CustomTitle) ? CustomTitle
        : !string.IsNullOrWhiteSpace(ShellTitle) ? ShellTitle
        : "wintty";
}

/// <summary>
/// The tabs of a window and the panes inside them. It owns no sessions: it says which panes exist, so the
/// window can start a session when one appears and dispose it when one goes.
/// </summary>
public sealed class TabSet
{
    private readonly List<Tab> _tabs = [];
    private int _nextTabId = 1;
    private int _nextPaneId = 1;

    /// <summary>The tabs in display order.</summary>
    public IReadOnlyList<Tab> Tabs => _tabs;

    /// <summary>Index of the selected tab, or -1 when there are none.</summary>
    public int SelectedIndex { get; private set; } = -1;

    /// <summary>The selected tab, or <see langword="null"/> when there are none (the window should close).</summary>
    public Tab? Selected => SelectedIndex < 0 ? null : _tabs[SelectedIndex];

    /// <summary>Opens a tab after the selected one and selects it.</summary>
    /// <returns>The new tab; its single pane is <see cref="Tab.Focused"/>.</returns>
    public Tab AddTab()
    {
        var tab = new Tab(_nextTabId++, new PaneId(_nextPaneId++));
        var index = SelectedIndex < 0 ? 0 : SelectedIndex + 1;
        _tabs.Insert(index, tab);
        SelectedIndex = index;
        return tab;
    }

    /// <summary>Closes a tab with all its panes.</summary>
    /// <param name="tabId">The tab.</param>
    /// <returns>The panes that were in it, so their sessions can be disposed; empty when the tab is unknown.</returns>
    public IReadOnlyList<PaneId> CloseTab(int tabId)
    {
        var index = _tabs.FindIndex(t => t.Id == tabId);
        if (index < 0)
        {
            return [];
        }

        var panes = PaneLayout.Leaves(_tabs[index].Root).ToList();
        _tabs.RemoveAt(index);
        if (_tabs.Count == 0)
        {
            SelectedIndex = -1;
        }
        else if (index < SelectedIndex || SelectedIndex >= _tabs.Count)
        {
            SelectedIndex = Math.Min(SelectedIndex - (index < SelectedIndex ? 1 : 0), _tabs.Count - 1);
        }

        return panes;
    }

    /// <summary>Selects a tab by position.</summary>
    /// <param name="index">The position; out-of-range values are ignored.</param>
    public void Select(int index)
    {
        if (index >= 0 && index < _tabs.Count)
        {
            SelectedIndex = index;
        }
    }

    /// <summary>Selects the next tab, wrapping from the last to the first.</summary>
    public void SelectNext()
    {
        if (_tabs.Count > 0)
        {
            SelectedIndex = (SelectedIndex + 1) % _tabs.Count;
        }
    }

    /// <summary>Selects the previous tab, wrapping from the first to the last.</summary>
    public void SelectPrevious()
    {
        if (_tabs.Count > 0)
        {
            SelectedIndex = (SelectedIndex - 1 + _tabs.Count) % _tabs.Count;
        }
    }

    /// <summary>Moves a tab to another position; the selection stays on the same tab.</summary>
    /// <param name="from">Current position.</param>
    /// <param name="to">New position.</param>
    public void Move(int from, int to)
    {
        if (from < 0 || from >= _tabs.Count || to < 0 || to >= _tabs.Count || from == to)
        {
            return;
        }

        var selected = Selected;
        var tab = _tabs[from];
        _tabs.RemoveAt(from);
        _tabs.Insert(to, tab);
        SelectedIndex = _tabs.IndexOf(selected!);
    }

    /// <summary>Gives a tab a name of the user's choosing, or clears it.</summary>
    /// <param name="tabId">The tab.</param>
    /// <param name="title">The name; blank restores the shell-provided title.</param>
    public void Rename(int tabId, string? title)
    {
        if (_tabs.Find(t => t.Id == tabId) is { } tab)
        {
            tab.CustomTitle = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        }
    }

    /// <summary>Records the title a program set for a pane; only the focused pane names its tab.</summary>
    /// <param name="pane">The pane that set the title.</param>
    /// <param name="title">The title.</param>
    public void SetShellTitle(PaneId pane, string? title)
    {
        if (TabOf(pane) is { } tab && tab.Focused == pane)
        {
            tab.ShellTitle = title;
        }
    }

    /// <summary>Splits a pane, focuses the new one and ends any zoom.</summary>
    /// <param name="pane">The pane to split.</param>
    /// <param name="direction">Which way to split.</param>
    /// <returns>The new pane, or <see langword="null"/> when <paramref name="pane"/> is unknown.</returns>
    public PaneId? Split(PaneId pane, SplitDirection direction)
    {
        if (TabOf(pane) is not { } tab)
        {
            return null;
        }

        var created = new PaneId(_nextPaneId++);
        tab.Root = PaneLayout.Split(tab.Root, pane, direction, created);
        tab.Focused = created;
        tab.Zoomed = null;
        return created;
    }

    /// <summary>Closes a pane. Closing a tab's last pane closes the tab.</summary>
    /// <param name="pane">The pane.</param>
    /// <returns>Whether the tab went with it.</returns>
    public bool ClosePane(PaneId pane)
    {
        if (TabOf(pane) is not { } tab)
        {
            return false;
        }

        var remaining = PaneLayout.Close(tab.Root, pane);
        if (remaining is null)
        {
            CloseTab(tab.Id);
            return true;
        }

        tab.Root = remaining;
        if (tab.Focused == pane)
        {
            tab.Focused = PaneLayout.Leaves(remaining).First();
        }

        if (tab.Zoomed == pane)
        {
            tab.Zoomed = null;
        }

        return false;
    }

    /// <summary>Focuses a pane.</summary>
    /// <param name="pane">The pane; unknown ids are ignored.</param>
    public void Focus(PaneId pane)
    {
        if (TabOf(pane) is { } tab)
        {
            tab.Focused = pane;
            tab.ShellTitle = null;
            if (tab.Zoomed is not null && tab.Zoomed != pane)
            {
                tab.Zoomed = pane;
            }
        }
    }

    /// <summary>Moves focus to the neighbouring pane of the selected tab.</summary>
    /// <param name="direction">Where to move.</param>
    /// <param name="bounds">The area the tab fills, in any units.</param>
    /// <returns>The pane now focused, or <see langword="null"/> when there is nothing in that direction.</returns>
    public PaneId? FocusNeighbour(FocusDirection direction, PaneRect bounds)
    {
        if (Selected is not { Zoomed: null } tab || PaneLayout.Neighbour(tab.Root, bounds, tab.Focused, direction) is not { } next)
        {
            return null;
        }

        Focus(next);
        return next;
    }

    /// <summary>Zooms the focused pane of the selected tab to fill it, or puts it back.</summary>
    public void ToggleZoom()
    {
        if (Selected is { } tab && PaneLayout.Leaves(tab.Root).Skip(1).Any())
        {
            tab.Zoomed = tab.Zoomed is null ? tab.Focused : null;
        }
    }

    /// <summary>Finds the tab that holds a pane.</summary>
    /// <param name="pane">The pane.</param>
    /// <returns>The tab, or <see langword="null"/> when no tab has it.</returns>
    public Tab? TabOf(PaneId pane) => _tabs.Find(t => PaneLayout.Leaves(t.Root).Contains(pane));
}
