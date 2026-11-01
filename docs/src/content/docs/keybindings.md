---
title: Keybindings
description: Default and custom keybindings.
---

Every action has a name you can bind in the [config file](/wintty/configuration/), and most have a default key. This table is generated from the action list in the code (`Keybinds`), so it always matches the app.

<!-- BEGIN GENERATED KEYBINDINGS -->
| Action | Default keys | Description |
| --- | --- | --- |
| `copy_to_clipboard` | `ctrl+shift+c`, `ctrl+insert` | Copy the selected text to the clipboard. |
| `paste_from_clipboard` | `ctrl+shift+v`, `shift+insert` | Paste the clipboard text into the program. |
| `new_tab` | `ctrl+shift+t` | Open a new tab. |
| `close_surface` | `ctrl+shift+w` | Close the focused pane, or its tab when it is the only pane. |
| `next_tab` | `ctrl+tab` | Switch to the next tab. |
| `previous_tab` | `ctrl+shift+tab` | Switch to the previous tab. |
| `new_split:right` | `ctrl+shift+d` | Split the focused pane, adding a new one to its right. |
| `new_split:down` | `ctrl+shift+e` | Split the focused pane, adding a new one below it. |
| `goto_split:left` | `ctrl+alt+left` | Move focus to the pane on the left. |
| `goto_split:right` | `ctrl+alt+right` | Move focus to the pane on the right. |
| `goto_split:up` | `ctrl+alt+up` | Move focus to the pane above. |
| `goto_split:down` | `ctrl+alt+down` | Move focus to the pane below. |
| `toggle_split_zoom` | `ctrl+shift+enter` | Make the focused pane fill the tab, or put it back. |
| `resize_pane:left` | `ctrl+alt+shift+left` | Move the divider next to the focused pane to the left. |
| `resize_pane:right` | `ctrl+alt+shift+right` | Move the divider next to the focused pane to the right. |
| `resize_pane:up` | `ctrl+alt+shift+up` | Move the divider next to the focused pane up. |
| `resize_pane:down` | `ctrl+alt+shift+down` | Move the divider next to the focused pane down. |
| `equalize_splits` | `ctrl+shift+equal` | Give every split in the tab an equal share. |
| `scroll_page_up` | `shift+page_up` | Scroll the history up by one screen. |
| `scroll_page_down` | `shift+page_down` | Scroll the history down by one screen. |
| `scroll_to_top` | `shift+home` | Scroll to the oldest line. |
| `scroll_to_bottom` | `shift+end` | Scroll back to the live screen. |
| `unbind` | none | Remove a binding, including a default one, so the key goes to the program. |
<!-- END GENERATED KEYBINDINGS -->

## Custom bindings

Add `keybind` lines to the config file, written as `trigger=action`:

```ini
keybind = ctrl+shift+n=new_tab
keybind = ctrl+shift+c=unbind
keybind = alt+f=scroll_page_down
```

- A trigger is any of `shift`, `ctrl`, `alt` and `super` plus one key, joined with `+`, in any order: `ctrl+alt+page_up`. Keys are letters, digits, `f1` to `f24`, and the names `enter`, `space`, `tab`, `escape`, `backspace`, `delete`, `insert`, `home`, `end`, `page_up`, `page_down`, `up`, `down`, `left`, `right`, `minus`, `equal`, `comma`, `period`, `semicolon`, `quote`, `slash`, `backslash`, `backquote`, `bracket_left` and `bracket_right`.
- `unbind` as the action removes a binding, including a default, so the key goes to the program.
- `keybind = clear` drops every default; `keybind =` forgets all `keybind` lines above it.
- If two lines bind the same key to different actions the later one wins and the earlier line is reported.
- Unknown keys and actions are reported and skipped. Saving the config file applies changes to open windows.
- Tab and split actions work in the window: new, close, switch and reorder tabs (drag them), double-click a tab to rename it, split a tab into panes, move focus between panes and zoom one to fill the tab. Pane sizes are fixed at half for now; dragging dividers is planned.

## Mouse, selection and clipboard

These work today. They are not rebindable.

| Gesture | Effect |
| --- | --- |
| Wheel | Scroll through history; a bar on the right edge shows where you are. Typing returns to the bottom. |
| Drag | Select text by character. Dragging past the top or bottom edge scrolls. |
| Double-click / triple-click | Select a word / a whole line, including rows a long line wrapped over. |
| `Alt`+drag | Select a rectangle. |
| `Ctrl+Shift+C` or `Ctrl+Insert` | Copy the selection. |
| `Ctrl+Shift+V` or `Shift+Insert`, middle-click | Paste. Programs that enabled bracketed paste get the text wrapped in paste markers. |
| Hold `Shift` | Bypass mouse reporting so selection works inside vim, tmux and similar. |

Pasting text with line breaks into a program that has not enabled bracketed paste asks for confirmation first; turn that off with `clipboard-paste-protection = false`.
