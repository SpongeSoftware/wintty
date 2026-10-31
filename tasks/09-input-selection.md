# 09 Input, clipboard, selection

Status: todo

**Goal:** a usable interactive terminal.

**Scope:** keyboard via libghostty key encoder (incl. kitty protocol), IME composition, mouse reporting, wheel scrollback, text selection (char/word/line/block), copy/paste with bracketed paste and multi-line warning, hyperlinks (OSC 8), URL click.

**Acceptance:** vttest keyboard/mouse sections pass; paste of multi-line text is bracketed; IME input works for Japanese.

**Files:** `src/Wintty.App/TerminalControl.*`, `src/Wintty.Core`.
