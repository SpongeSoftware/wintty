using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wintty.Core;
using Wintty.Core.Keybindings;
using Wintty.Core.Layout;

namespace Wintty.App;

/// <summary>
/// The window: a strip of tabs, each holding one or more terminal panes. <see cref="TabSet"/> decides what exists;
/// this class keeps the controls in step with it and carries out the tab and split actions.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly TabSet _model = new();
    private readonly Dictionary<PaneId, TerminalControl> _terminals = [];
    private readonly Dictionary<int, Grid> _hosts = [];
    private bool _syncing;
    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"wintty {VersionInfo.Current}";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "wintty.ico"));
        Closed += (_, _) => DisposeAll();
        AddTab();
    }

    private void AddTab()
    {
        // A new tab starts where the pane you were in is, when its shell reports a directory.
        string? startIn = _model.Selected is { } current && _terminals.TryGetValue(current.Focused, out global::Wintty.App.TerminalControl? source) ? source.WorkingDirectory : null;
        Tab tab = _model.AddTab();
        TerminalControl terminal = CreateTerminal(tab.Focused, startIn);
        var host = new Grid();
        _hosts[tab.Id] = host;
        var item = new TabViewItem { Tag = tab.Id, Header = tab.Title, Content = host };
        item.DoubleTapped += (_, _) => _ = RenameAsync(tab);
        _syncing = true;
        Tabs.TabItems.Add(item);
        Tabs.SelectedItem = item;
        _syncing = false;
        Rebuild(tab);
        terminal.FocusTerminal();
        UpdateTitles();
    }

    private TerminalControl CreateTerminal(PaneId pane, string? startIn = null)
    {
        var terminal = new TerminalControl { InitialDirectory = startIn };
        _terminals[pane] = terminal;
        terminal.TitleChanged += (_, title) =>
        {
            _model.SetShellTitle(pane, title);
            UpdateTitles();
        };
        terminal.ProcessExited += (_, _) => ClosePane(pane);
        terminal.PaneActivated += (_, _) =>
        {
            _model.Focus(pane);
            UpdateTitles();
        };
        terminal.ActionRequested += (_, action) => Execute(pane, action);
        return terminal;
    }

    private void Execute(PaneId pane, TerminalAction action)
    {
        switch (action)
        {
            case TerminalAction.NewTab:
                AddTab();
                break;
            case TerminalAction.CloseSurface:
                ClosePane(pane);
                break;
            case TerminalAction.NextTab:
                _model.SelectNext();
                SelectModelTab();
                break;
            case TerminalAction.PreviousTab:
                _model.SelectPrevious();
                SelectModelTab();
                break;
            case TerminalAction.NewSplitRight:
                Split(pane, SplitDirection.Right);
                break;
            case TerminalAction.NewSplitDown:
                Split(pane, SplitDirection.Down);
                break;
            case TerminalAction.GotoSplitLeft:
                GotoSplit(FocusDirection.Left);
                break;
            case TerminalAction.GotoSplitRight:
                GotoSplit(FocusDirection.Right);
                break;
            case TerminalAction.GotoSplitUp:
                GotoSplit(FocusDirection.Up);
                break;
            case TerminalAction.GotoSplitDown:
                GotoSplit(FocusDirection.Down);
                break;
            case TerminalAction.ToggleFullscreen:
                AppWindow.SetPresenter(
                    AppWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen
                        ? Microsoft.UI.Windowing.AppWindowPresenterKind.Default
                        : Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                break;
            case TerminalAction.NewWindow:
                if (Environment.ProcessPath is { } exe)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false });
                }

                break;
            case TerminalAction.ResizePaneLeft:
                ResizeFocused(FocusDirection.Left);
                break;
            case TerminalAction.ResizePaneRight:
                ResizeFocused(FocusDirection.Right);
                break;
            case TerminalAction.ResizePaneUp:
                ResizeFocused(FocusDirection.Up);
                break;
            case TerminalAction.ResizePaneDown:
                ResizeFocused(FocusDirection.Down);
                break;
            case TerminalAction.EqualizeSplits:
                _model.EqualizeSplits();
                if (_model.Selected is { } equalised)
                {
                    Rebuild(equalised);
                    FocusPane(equalised.Focused);
                }

                break;
            case TerminalAction.ToggleSplitZoom:
                _model.ToggleZoom();
                if (_model.Selected is { } zoomed)
                {
                    Rebuild(zoomed);
                    FocusPane(zoomed.Focused);
                }

                break;
        }
    }

    private void ResizeFocused(FocusDirection direction)
    {
        if (_model.ResizeFocused(direction) && _model.Selected is { } tab)
        {
            Rebuild(tab);
            FocusPane(tab.Focused);
        }
    }

    private void Split(PaneId pane, SplitDirection direction)
    {
        if (_model.TabOf(pane) is not { } tab || _model.Split(pane, direction) is not { } created)
        {
            return;
        }

        CreateTerminal(created, _terminals.TryGetValue(pane, out TerminalControl? source) ? source.WorkingDirectory : null);
        Rebuild(tab);
        FocusPane(created);
    }

    private void GotoSplit(FocusDirection direction)
    {
        if (_model.Selected is not { } tab || !_hosts.TryGetValue(tab.Id, out Grid? host))
        {
            return;
        }

        if (_model.FocusNeighbour(direction, new PaneRect(0, 0, host.ActualWidth, host.ActualHeight)) is { } next)
        {
            FocusPane(next);
        }
    }

    private void ClosePane(PaneId pane)
    {
        if (_closing || !_terminals.ContainsKey(pane) || _model.TabOf(pane) is not { } tab)
        {
            return;
        }

        bool tabClosed = _model.ClosePane(pane);
        DisposeTerminal(pane);
        if (tabClosed)
        {
            RemoveTabItem(tab.Id);
            AfterTabsChanged();
            return;
        }

        Rebuild(tab);
        FocusPane(tab.Focused);
        UpdateTitles();
    }

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is not int tabId)
        {
            return;
        }

        foreach (PaneId pane in _model.CloseTab(tabId))
        {
            DisposeTerminal(pane);
        }

        RemoveTabItem(tabId);
        AfterTabsChanged();
    }

    private void AfterTabsChanged()
    {
        if (_model.Tabs.Count == 0)
        {
            Close();
            return;
        }

        SelectModelTab();
    }

    private void RemoveTabItem(int tabId)
    {
        _hosts.Remove(tabId);
        TabViewItem? item = Tabs.TabItems.OfType<TabViewItem>().FirstOrDefault(i => i.Tag is int id && id == tabId);
        if (item is not null)
        {
            _syncing = true;
            Tabs.TabItems.Remove(item);
            _syncing = false;
        }
    }

    private void OnAddTabClicked(TabView sender, object args) => AddTab();

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || Tabs.SelectedItem is not TabViewItem { Tag: int tabId })
        {
            return;
        }

        int index = _model.Tabs.ToList().FindIndex(t => t.Id == tabId);
        _model.Select(index);
        if (_model.Selected is { } tab)
        {
            FocusPane(tab.Focused);
        }

        UpdateTitles();
    }

    // Dragging a tab removes and re-inserts its item, so match the model to whatever order the strip now has.
    private void OnTabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        var order = Tabs.TabItems.OfType<TabViewItem>().Select(i => i.Tag).OfType<int>().ToList();
        if (order.Count != _model.Tabs.Count)
        {
            return;
        }

        for (int target = 0; target < order.Count; target++)
        {
            int current = _model.Tabs.ToList().FindIndex(t => t.Id == order[target]);
            if (current >= 0 && current != target)
            {
                _model.Move(current, target);
            }
        }
    }

    private void SelectModelTab()
    {
        if (_model.Selected is not { } tab)
        {
            return;
        }

        TabViewItem? item = Tabs.TabItems.OfType<TabViewItem>().FirstOrDefault(i => i.Tag is int id && id == tab.Id);
        if (item is not null && !ReferenceEquals(Tabs.SelectedItem, item))
        {
            _syncing = true;
            Tabs.SelectedItem = item;
            _syncing = false;
        }

        FocusPane(tab.Focused);
        UpdateTitles();
    }

    private void FocusPane(PaneId pane)
    {
        if (_terminals.TryGetValue(pane, out TerminalControl? terminal))
        {
            _model.Focus(pane);
            terminal.FocusTerminal();
        }
    }

    private void UpdateTitles()
    {
        foreach (TabViewItem item in Tabs.TabItems.OfType<TabViewItem>())
        {
            if (item.Tag is int id && _model.Tabs.FirstOrDefault(t => t.Id == id) is { } tab)
            {
                item.Header = tab.Title;
            }
        }

        if (_model.Selected is { } selected)
        {
            Title = selected.Title;
        }
    }

    // Lays the tab's panes out in nested grids. Terminals keep running while detached, so a pane that is
    // hidden by zoom or moved by a split is never restarted.
    private void Rebuild(Tab tab)
    {
        if (!_hosts.TryGetValue(tab.Id, out Grid? host))
        {
            return;
        }

        foreach (PaneId pane in PaneLayout.Leaves(tab.Root))
        {
            if (_terminals[pane].Parent is Panel parent)
            {
                parent.Children.Remove(_terminals[pane]);
            }
        }

        host.Children.Clear();
        host.Children.Add(tab.Zoomed is { } zoomed ? _terminals[zoomed] : Build(tab.Root));
    }

    private UIElement Build(PaneNode node)
    {
        if (node is PaneLeaf leaf)
        {
            return _terminals[leaf.Id];
        }

        var split = (PaneSplit)node;
        var grid = new Grid();
        UIElement first = Build(split.First);
        UIElement second = Build(split.Second);
        var divider = new Border { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 70, 70, 70)) };
        if (split.Direction == SplitDirection.Right)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(split.Ratio, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - split.Ratio, GridUnitType.Star) });
            divider.Width = 1;
            Grid.SetColumn(divider, 1);
            Grid.SetColumn((FrameworkElement)second, 2);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(split.Ratio, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - split.Ratio, GridUnitType.Star) });
            divider.Height = 1;
            Grid.SetRow(divider, 1);
            Grid.SetRow((FrameworkElement)second, 2);
        }

        grid.Children.Add(first);
        grid.Children.Add(divider);
        grid.Children.Add(second);
        return grid;
    }

    private async Task RenameAsync(Tab tab)
    {
        var box = new TextBox { Text = tab.CustomTitle ?? tab.Title, SelectionStart = 0 };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Rename tab",
            Content = box,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _model.Rename(tab.Id, box.Text);
            UpdateTitles();
        }

        FocusPane(tab.Focused);
    }

    private void DisposeTerminal(PaneId pane)
    {
        if (_terminals.Remove(pane, out TerminalControl? terminal))
        {
            if (terminal.Parent is Panel parent)
            {
                parent.Children.Remove(terminal);
            }

            terminal.Close();
        }
    }

    private void DisposeAll()
    {
        _closing = true;
        foreach (TerminalControl terminal in _terminals.Values)
        {
            terminal.Close();
        }

        _terminals.Clear();
    }
}
