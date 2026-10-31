# 06 libghostty-vt binding

Status: todo

**Goal:** managed API over libghostty-vt in `Wintty.Core`.

**Scope:** evaluate `LibGhostty.Net`; if insufficient, build ghostty-vt for win-x64/arm64 with zig (pinned ref, script in `scripts/`) and P/Invoke it. Expose: create terminal, write bytes, resize, read render state (rows, cells, styles, cursor, dirty rows), scrollback, key/mouse encoders, title/bell/clipboard callbacks.

**Acceptance:** unit test feeds `"\e[31mhi\e[0m"` and asserts cell content and colour; DLL is copied to app output for both architectures; pinned version documented in NOTICE.

**Files:** `src/Wintty.Core`, `scripts/build-ghostty-vt.ps1`, `tests/`.
