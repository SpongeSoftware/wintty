# 15 Windows arm64 support (deferred)

Status: deferred

**Why deferred:** `LibGhostty.Net` 1.0.x ships natives for win-x64 only (`ghostty-vt.dll`, `conpty.dll`, `OpenConsole.exe`).

**Scope when picked up:** check whether a newer LibGhostty.Net adds win-arm64; otherwise build ghostty-vt for `aarch64-windows` with Zig and obtain arm64 ConPTY/OpenConsole from Windows Terminal; restore `ARM64` platform and `win-arm64` RID in `Wintty.App.csproj`, the release matrix, SBOM loop and winget `ARCHES`; test on real arm64 hardware.

**Acceptance:** arm64 zip builds, runs a shell, and is listed in the release and winget manifest.
