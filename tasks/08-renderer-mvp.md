# 08 Renderer MVP

Status: todo

**Goal:** draw the terminal grid in a WinUI 3 `SwapChainPanel`.

**Scope:** D3D11 device/swap chain, DirectWrite font metrics and glyph atlas, instanced cell quads (fg/bg/underline/strike), cursor styles, 24-bit colour, wide chars/emoji, font fallback, DPI scaling, redraw only on dirty rows, 60+ fps under heavy output.

**Acceptance:** `pwsh` prompt, `nvim`, `htop` (WSL) render correctly; colour test script passes; resizing the window reflows with no flicker.

**Files:** `src/Wintty.Render`, `src/Wintty.App/TerminalControl.*`.
