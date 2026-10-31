---
title: Architecture
description: How wintty is put together.
---

- **Wintty.Core** defines the abstractions (`ITerminalEngine`, `IPtyConnection` and their factories) and `TerminalSession`, which connects a process to a terminal engine. It has no native or UI dependencies.
- **Wintty.Ghostty** is the single gateway to libghostty-vt and the `LibGhostty.Net` package: `GhosttyGateway` implements the Core factories, including ConPTY process hosting. No other project may reference the package.
- **Wintty.Input** maps Windows virtual-key codes to Core's `TerminalKey` (pure, unit tested). Key events are encoded into bytes by libghostty's key encoder through the gateway, so program modes such as application cursor keys and the kitty keyboard protocol are honoured.
- **Wintty.Render** draws the grid with Direct3D 11 and DirectWrite in a `SwapChainPanel`.
- **Wintty.App** is the WinUI 3 shell: tabs, a recursive split tree, config and keybindings, and the composition root that wires `GhosttyGateway` into Core.

Data flow: shell -> ConPTY -> `TerminalSession` -> `ITerminalEngine.Write` -> render state -> renderer; keyboard -> key encoder -> ConPTY.

Windows x64 is supported; arm64 is deferred until the native package ships arm64 binaries.

## Terminal queries

Programs ask the terminal questions by writing escape sequences, and wait for a reply on their input. libghostty produces the replies and wintty forwards them to the running program (in order with anything you type).

| Query | Sequence | Answered |
| --- | --- | --- |
| Device attributes | `CSI c`, `CSI > c`, `CSI = c` | yes |
| Device and cursor status | `CSI 5 n`, `CSI 6 n` | yes |
| Terminal version | `CSI > q` (XTVERSION) | yes (`libghostty`) |
| Mode state | `CSI ? Pm $ p` (DECRQM) | yes |
| Kitty keyboard flags | `CSI ? u` | yes |
| Window, text area and cell size | `CSI 14 t`, `CSI 18 t`, `CSI 16 t` | yes; pixel sizes come from the measured cell, and read 0 until it is known |
| Colours | OSC 4, OSC 10, OSC 11 (`?`) | not yet: libghostty-vt does not reply |
| Status string | DECRQSS | not yet |

The missing colour queries mean programs that probe the background colour (to choose a light or dark theme) fall back to their default.
