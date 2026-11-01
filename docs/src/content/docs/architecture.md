---
title: Architecture
description: How wintty is put together.
---

## Projects

```text
Wintty.App        WinUI 3 shell, Win2D drawing, pointer and keyboard handling
   |  \
   |   +--> Wintty.Input     Windows virtual keys -> TerminalKey (pure)
   |   +--> Wintty.Render    snapshot -> render plan, grid sizing, hit testing (pure)
   |   +--> Wintty.Ghostty   the only project that touches libghostty and ConPTY
   v
Wintty.Core       abstractions, sessions, config, themes, selection (no native or UI code)
```

Dependencies point one way: `App` -> `Render`, `Ghostty`, `Input` -> `Core`. Architecture tests fail the build if that is violated.

- **Wintty.Core** defines the abstractions (`ITerminalEngine`, `IPtyConnection` and their factories), `TerminalSession`, which connects a process to a terminal engine, the config file parser and watcher, the built-in themes, and selection and resize logic. It has no native or UI dependencies.
- **Wintty.Ghostty** is the single gateway to libghostty-vt and the `LibGhostty.Net` package: `GhosttyGateway` implements the Core factories, including ConPTY process hosting. No other project may reference the package. Where the wrapper lacks a native call (for example reading terminal modes) the gateway binds it directly.
- **Wintty.Input** maps Windows virtual-key codes to Core's `TerminalKey` (pure, unit tested). Key events are encoded into bytes by libghostty's key encoder through the gateway, so program modes such as application cursor keys and the kitty keyboard protocol are honoured.
- **Wintty.Render** is pure .NET: `RenderPlanner` turns an immutable `TerminalSnapshot` into a `RenderPlan` (background runs, text runs, decorations, cursor, scroll thumb), `GridSizer` computes the grid and `CellHitTester` maps pixels to cells. It has no graphics API dependency.
- **Wintty.App** is the thin WinUI 3 shell. `TerminalControl` draws a render plan with Win2D and forwards keyboard, pointer and resize events to the session. Keep logic out of it.

## From shell to screen

```text
shell -> ConPTY -> TerminalSession pump -> ITerminalEngine.Write
                                              |
                      libghostty screen, scrollback and modes
                                              |
                 CreateSnapshot (resolved colours, cursor, scrollbar)
                                              |
                       RenderPlanner -> RenderPlan -> Win2D in TerminalControl

keyboard -> VirtualKeyMap -> libghostty key encoder -> ConPTY
mouse    -> mouse encoder (when the program asked for reports) -> ConPTY
```

Engine access is serialised: `TerminalSession.SyncRoot` guards the engine against the output pump, and writes to the process share one lock so replies and typing never interleave.

## Decisions and vocabulary

The reasons behind the larger choices are recorded as [architecture decision records](https://github.com/SpongeSoftware/wintty/tree/main/docs/adr), and the project's terms are defined in the [glossary](https://github.com/SpongeSoftware/wintty/blob/main/GLOSSARY.md).

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
