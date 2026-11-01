namespace Wintty.Core.Keybindings;

/// <summary>Something a key binding can make wintty do.</summary>
public enum TerminalAction
{
    /// <summary>Copy the selected text to the clipboard.</summary>
    CopyToClipboard = 0,

    /// <summary>Paste the clipboard text into the program.</summary>
    PasteFromClipboard,

    /// <summary>Open a new tab.</summary>
    NewTab,

    /// <summary>Close the focused pane, or its tab when it is the only pane.</summary>
    CloseSurface,

    /// <summary>Switch to the next tab.</summary>
    NextTab,

    /// <summary>Switch to the previous tab.</summary>
    PreviousTab,

    /// <summary>Split the focused pane, adding a new one to its right.</summary>
    NewSplitRight,

    /// <summary>Split the focused pane, adding a new one below it.</summary>
    NewSplitDown,

    /// <summary>Move focus to the pane on the left.</summary>
    GotoSplitLeft,

    /// <summary>Move focus to the pane on the right.</summary>
    GotoSplitRight,

    /// <summary>Move focus to the pane above.</summary>
    GotoSplitUp,

    /// <summary>Move focus to the pane below.</summary>
    GotoSplitDown,

    /// <summary>Make the focused pane fill the tab, or put it back.</summary>
    ToggleSplitZoom,

    /// <summary>Make the text of the focused pane larger.</summary>
    IncreaseFontSize,

    /// <summary>Make the text of the focused pane smaller.</summary>
    DecreaseFontSize,

    /// <summary>Return the focused pane to the configured font size.</summary>
    ResetFontSize,

    /// <summary>Switch the window between full screen and normal.</summary>
    ToggleFullscreen,

    /// <summary>Open another wintty window.</summary>
    NewWindow,

    /// <summary>Move the divider next to the focused pane to the left.</summary>
    ResizePaneLeft,

    /// <summary>Move the divider next to the focused pane to the right.</summary>
    ResizePaneRight,

    /// <summary>Move the divider next to the focused pane up.</summary>
    ResizePaneUp,

    /// <summary>Move the divider next to the focused pane down.</summary>
    ResizePaneDown,

    /// <summary>Give every split in the tab an equal share.</summary>
    EqualizeSplits,

    /// <summary>Scroll the history up by one screen.</summary>
    ScrollPageUp,

    /// <summary>Scroll the history down by one screen.</summary>
    ScrollPageDown,

    /// <summary>Scroll to the oldest line.</summary>
    ScrollToTop,

    /// <summary>Scroll back to the live screen.</summary>
    ScrollToBottom,

    /// <summary>Removes a binding, including a default one; the key then goes to the program.</summary>
    Unbind,
}
