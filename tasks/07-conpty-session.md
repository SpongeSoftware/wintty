# 07 ConPTY session

Status: todo

**Goal:** spawn a shell and pump bytes between ConPTY and the terminal core.

**Scope:** `Wintty.Pty`: `CreatePseudoConsole`, attribute list, process creation, async read loop, write, resize (debounced), exit notification; bundle OpenConsole/conpty.dll if needed for modern VT passthrough; `TerminalSession` combining PTY + core terminal.

**Acceptance:** integration test runs `cmd /c echo hi` and sees `hi` in the grid; resize while `pwsh` runs does not corrupt output (see ghostty-org/ghostty discussion #14291); clean shutdown with no leaked handles.

**Files:** `src/Wintty.Core/Sessions/TerminalSession.cs`, `src/Wintty.Ghostty`.

**Update:** `LibGhostty.Net` hosts ConPTY itself, so the separate `Wintty.Pty` project was removed. This task is now: the bits of PTY behaviour still wanted on top (debounced resize to work around ghostty-org/ghostty discussion #14291, shell profiles, working-directory inheritance), implemented in `Wintty.Core` against `IPtyConnection`.
