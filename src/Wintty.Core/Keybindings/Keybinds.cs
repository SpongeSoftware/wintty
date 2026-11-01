using System.Text;
using Wintty.Core.Configuration;
using Wintty.Core.Input;

namespace Wintty.Core.Keybindings;

/// <summary>Documents one action for the generated keybinding reference.</summary>
/// <param name="Action">The action.</param>
/// <param name="Name">The name used in the config file, for example <c>new_split:right</c>.</param>
/// <param name="Description">What the action does.</param>
public sealed record ActionInfo(TerminalAction Action, string Name, string Description);

/// <summary>A binding as written in the config file, remembered with its line for diagnostics.</summary>
/// <param name="Trigger">The key and modifiers.</param>
/// <param name="Action">What it does.</param>
/// <param name="Line">One-based line in the config file.</param>
public sealed record KeybindEntry(KeyTrigger Trigger, TerminalAction Action, int Line);

/// <summary>The names and defaults of all key bindings, and the parser for <c>keybind</c> lines.</summary>
public static class Keybinds
{
    /// <summary>Every action a binding can name, in the order the reference lists them.</summary>
    public static IReadOnlyList<ActionInfo> Actions { get; } =
    [
        new(TerminalAction.CopyToClipboard, "copy_to_clipboard", "Copy the selected text to the clipboard."),
        new(TerminalAction.PasteFromClipboard, "paste_from_clipboard", "Paste the clipboard text into the program."),
        new(TerminalAction.NewTab, "new_tab", "Open a new tab."),
        new(TerminalAction.CloseSurface, "close_surface", "Close the focused pane, or its tab when it is the only pane."),
        new(TerminalAction.NextTab, "next_tab", "Switch to the next tab."),
        new(TerminalAction.PreviousTab, "previous_tab", "Switch to the previous tab."),
        new(TerminalAction.NewSplitRight, "new_split:right", "Split the focused pane, adding a new one to its right."),
        new(TerminalAction.NewSplitDown, "new_split:down", "Split the focused pane, adding a new one below it."),
        new(TerminalAction.GotoSplitLeft, "goto_split:left", "Move focus to the pane on the left."),
        new(TerminalAction.GotoSplitRight, "goto_split:right", "Move focus to the pane on the right."),
        new(TerminalAction.GotoSplitUp, "goto_split:up", "Move focus to the pane above."),
        new(TerminalAction.GotoSplitDown, "goto_split:down", "Move focus to the pane below."),
        new(TerminalAction.ToggleSplitZoom, "toggle_split_zoom", "Make the focused pane fill the tab, or put it back."),
        new(TerminalAction.ResizePaneLeft, "resize_pane:left", "Move the divider next to the focused pane to the left."),
        new(TerminalAction.ResizePaneRight, "resize_pane:right", "Move the divider next to the focused pane to the right."),
        new(TerminalAction.ResizePaneUp, "resize_pane:up", "Move the divider next to the focused pane up."),
        new(TerminalAction.ResizePaneDown, "resize_pane:down", "Move the divider next to the focused pane down."),
        new(TerminalAction.EqualizeSplits, "equalize_splits", "Give every split in the tab an equal share."),
        new(TerminalAction.ScrollPageUp, "scroll_page_up", "Scroll the history up by one screen."),
        new(TerminalAction.ScrollPageDown, "scroll_page_down", "Scroll the history down by one screen."),
        new(TerminalAction.ScrollToTop, "scroll_to_top", "Scroll to the oldest line."),
        new(TerminalAction.ScrollToBottom, "scroll_to_bottom", "Scroll back to the live screen."),
        new(TerminalAction.Unbind, "unbind", "Remove a binding, including a default one, so the key goes to the program."),
    ];

    /// <summary>The bindings that ship with wintty.</summary>
    public static IReadOnlyList<(KeyTrigger Trigger, TerminalAction Action)> Defaults { get; } =
    [
        (Trigger("ctrl+shift+c"), TerminalAction.CopyToClipboard),
        (Trigger("ctrl+insert"), TerminalAction.CopyToClipboard),
        (Trigger("ctrl+shift+v"), TerminalAction.PasteFromClipboard),
        (Trigger("shift+insert"), TerminalAction.PasteFromClipboard),
        (Trigger("ctrl+shift+t"), TerminalAction.NewTab),
        (Trigger("ctrl+shift+w"), TerminalAction.CloseSurface),
        (Trigger("ctrl+tab"), TerminalAction.NextTab),
        (Trigger("ctrl+shift+tab"), TerminalAction.PreviousTab),
        (Trigger("ctrl+shift+d"), TerminalAction.NewSplitRight),
        (Trigger("ctrl+shift+e"), TerminalAction.NewSplitDown),
        (Trigger("ctrl+alt+left"), TerminalAction.GotoSplitLeft),
        (Trigger("ctrl+alt+right"), TerminalAction.GotoSplitRight),
        (Trigger("ctrl+alt+up"), TerminalAction.GotoSplitUp),
        (Trigger("ctrl+alt+down"), TerminalAction.GotoSplitDown),
        (Trigger("ctrl+shift+enter"), TerminalAction.ToggleSplitZoom),
        (Trigger("ctrl+alt+shift+left"), TerminalAction.ResizePaneLeft),
        (Trigger("ctrl+alt+shift+right"), TerminalAction.ResizePaneRight),
        (Trigger("ctrl+alt+shift+up"), TerminalAction.ResizePaneUp),
        (Trigger("ctrl+alt+shift+down"), TerminalAction.ResizePaneDown),
        (Trigger("ctrl+shift+equal"), TerminalAction.EqualizeSplits),
        (Trigger("shift+page_up"), TerminalAction.ScrollPageUp),
        (Trigger("shift+page_down"), TerminalAction.ScrollPageDown),
        (Trigger("shift+home"), TerminalAction.ScrollToTop),
        (Trigger("shift+end"), TerminalAction.ScrollToBottom),
    ];

    /// <summary>Reads an action name such as <c>new_split:right</c>.</summary>
    /// <param name="name">The name, case-insensitive.</param>
    /// <param name="action">The action when parsing succeeded.</param>
    /// <returns><see langword="true"/> when the name is a known action.</returns>
    public static bool TryParseAction(string name, out TerminalAction action)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var info in Actions)
        {
            if (string.Equals(info.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                action = info.Action;
                return true;
            }
        }

        action = default;
        return false;
    }

    /// <summary>Gets the config-file name of an action.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The name, for example <c>new_tab</c>.</returns>
    public static string NameOf(TerminalAction action) => Actions.First(a => a.Action == action).Name;

    /// <summary>Reads the value of a <c>keybind</c> line, <c>trigger=action</c>.</summary>
    /// <param name="value">The text after <c>keybind =</c>.</param>
    /// <param name="line">The line it came from, kept for later diagnostics.</param>
    /// <param name="entry">The binding when parsing succeeded.</param>
    /// <param name="error">Why parsing failed.</param>
    /// <returns><see langword="true"/> when the value is a valid binding.</returns>
    public static bool TryParseEntry(string value, int line, out KeybindEntry? entry, out string? error)
    {
        ArgumentNullException.ThrowIfNull(value);
        entry = null;
        var separator = value.LastIndexOf('=');
        if (separator <= 0 || separator == value.Length - 1)
        {
            error = "expected keybind = trigger=action";
            return false;
        }

        if (!KeyTrigger.TryParse(value[..separator], out var trigger, out error))
        {
            return false;
        }

        var actionName = value[(separator + 1)..].Trim();
        if (!TryParseAction(actionName, out var action))
        {
            error = $"unknown action '{actionName}'";
            return false;
        }

        entry = new KeybindEntry(trigger, action, line);
        return true;
    }

    /// <summary>
    /// Combines the defaults with the user's bindings. A later line replaces an earlier one for the same key,
    /// and the replacement is reported; <see cref="TerminalAction.Unbind"/> removes a binding.
    /// </summary>
    /// <param name="user">The user's bindings in file order.</param>
    /// <param name="clearDefaults">Start from nothing instead of the defaults.</param>
    /// <param name="diagnostics">Receives a note for each binding replaced by a later line.</param>
    /// <returns>The bindings in force.</returns>
    public static IReadOnlyDictionary<KeyTrigger, TerminalAction> Resolve(
        IEnumerable<KeybindEntry> user, bool clearDefaults, ICollection<ConfigDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(diagnostics);
        var map = new Dictionary<KeyTrigger, TerminalAction>();
        if (!clearDefaults)
        {
            foreach (var (trigger, action) in Defaults)
            {
                map[trigger] = action;
            }
        }

        var fromUser = new Dictionary<KeyTrigger, KeybindEntry>();
        foreach (var entry in user)
        {
            if (fromUser.TryGetValue(entry.Trigger, out var earlier) && earlier.Action != entry.Action)
            {
                diagnostics.Add(new ConfigDiagnostic(
                    entry.Line, $"{entry.Trigger} is already bound on line {earlier.Line}; this line replaces it"));
            }

            fromUser[entry.Trigger] = entry;
            if (entry.Action == TerminalAction.Unbind)
            {
                map.Remove(entry.Trigger);
            }
            else
            {
                map[entry.Trigger] = entry.Action;
            }
        }

        return map;
    }

    /// <summary>Renders the actions and default bindings as the Markdown used on the reference page.</summary>
    /// <returns>Two tables, ending with a newline.</returns>
    public static string RenderReference()
    {
        var builder = new StringBuilder();
        builder.Append("| Action | Default keys | Description |\n| --- | --- | --- |\n");
        foreach (var info in Actions)
        {
            var keys = Defaults.Where(d => d.Action == info.Action).Select(d => "`" + d.Trigger + "`").ToList();
            builder.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"| `{info.Name}` | {(keys.Count == 0 ? "none" : string.Join(", ", keys))} | {info.Description} |\n");
        }

        return builder.ToString();
    }

    private static KeyTrigger Trigger(string text) =>
        KeyTrigger.TryParse(text, out var trigger, out _) ? trigger : throw new InvalidOperationException("Bad default binding " + text);
}
