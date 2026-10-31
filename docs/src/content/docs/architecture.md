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
