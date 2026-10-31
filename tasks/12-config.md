# 12 Config and hot reload

Status: todo

**Goal:** Ghostty-style configuration.

**Scope:** parser for `key = value` (comments, repeated keys, quoted values), `%APPDATA%\wintty\config`, file watcher with debounce, validation errors surfaced in UI, options: font-family/size, theme + palette, cursor-style/blink, scrollback-limit, command/shell, window padding, `keybind` bindings; updates `docs/.../configuration.md`.

**Acceptance:** unit tests for parser; editing the file changes font/theme live; invalid lines are reported but don't crash.

**Files:** `src/Wintty.Core/Config/*`, `tests/`, docs.
