---
title: Keybindings
description: Default and custom keybindings.
---

| Action | Default |
| --- | --- |
| New tab | `Ctrl+Shift+T` |
| Close pane/tab | `Ctrl+Shift+W` |
| Split right | `Ctrl+Shift+D` |
| Split down | `Ctrl+Alt+D` |
| Copy / Paste | `Ctrl+Shift+C` / `Ctrl+Shift+V` |

Custom bindings use `keybind = ctrl+shift+n=new_window`. These defaults are provisional.

## Mouse, selection and clipboard

These work today.

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
