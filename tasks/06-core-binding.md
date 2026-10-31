# 06 libghostty-vt binding

Status: partly done (abstractions, gateway, PTY hosting, title; render state, key and mouse encoders still to do)

**Goal:** managed API over libghostty-vt: abstractions in `Wintty.Core`, the single gateway in `Wintty.Ghostty`.

**Scope:** evaluate `LibGhostty.Net`; if insufficient, build ghostty-vt for win-x64 with zig (arm64 deferred, task 15) (pinned ref, script in `scripts/`) and P/Invoke it. Expose: create terminal, write bytes, resize, read render state (rows, cells, styles, cursor, dirty rows), scrollback, key/mouse encoders, title/bell/clipboard callbacks.

**Constraint:** all libghostty / NuGet access goes through ONE gateway class implementing `ITerminalEngine` (see task 14). Nothing else may reference the native library or package; add the architecture test in the same change. Full unit tests using a fake engine for consumers, and tests for the adapter itself.

**Acceptance:** unit test feeds `"\e[31mhi\e[0m"` and asserts cell content and colour; DLL is copied to app output for both architectures; pinned version documented in NOTICE.

**Files:** `src/Wintty.Core`, `scripts/build-ghostty-vt.ps1`, `tests/`.
