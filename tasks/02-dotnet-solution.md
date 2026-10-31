# 02 .NET solution and tag-derived versioning

Status: done (retargeted to .NET 10)

**Goal:** buildable solution with the version injected from the git tag.

**Scope:** `wintty.sln`; projects `src/Wintty.Core`, `Wintty.Ghostty`, `Wintty.Render` (class libraries), `src/Wintty.App` (WinUI 3, unpackaged, self-contained), `tests/Wintty.Tests` (xUnit). `Directory.Build.props` already holds defaults.

**Acceptance:**
- `dotnet build wintty.sln` and `dotnet test` pass on windows-latest.
- `dotnet build -p:Version=1.2.3` yields assembly/file version 1.2.3; no version is committed per release.
- App shows its version (from assembly informational version) in an About dialog or `--version`.

**Files:** `wintty.sln`, `src/*`, `tests/*`, `Directory.Build.props`.
