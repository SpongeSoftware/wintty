---
title: Architecture
description: How wintty is put together.
---

- **Wintty.Core** wraps libghostty-vt (terminal state, parser, key encoder).
- **Wintty.Pty** wraps ConPTY and spawns shells; resize is debounced to avoid ConPTY reflow issues.
- **Wintty.Render** draws the grid with Direct3D 11 and DirectWrite in a `SwapChainPanel`.
- **Wintty.App** is the WinUI 3 shell: tabs, a recursive split tree, config and keybindings.

Data flow: shell -> ConPTY -> `ghostty_terminal_write` -> render state -> renderer; keyboard -> key encoder -> ConPTY.
