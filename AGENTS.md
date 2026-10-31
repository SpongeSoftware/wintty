# AGENTS.md

Instructions for AI coding agents working on wintty. Written to be tool-agnostic: it applies to Claude Code, Codex, Cursor and local models alike. `CLAUDE.md` imports this file.

## What this is

wintty is a native Windows terminal emulator. It uses libghostty-vt (Ghostty's terminal core) through the `LibGhostty.Net` NuGet package, renders with WinUI 3 and Win2D, and hosts shells through ConPTY. It is written in C# on .NET 10. Only Windows x64 is supported; arm64 is deferred until the native package ships arm64 binaries. It is an independent project, not affiliated with Ghostty.

## Commands

Run from the repository root. The shell is Git Bash or PowerShell on Windows.

| Task | Command |
| --- | --- |
| Build everything | `dotnet build wintty.sln -c Release` |
| Build as CI does (warnings are errors) | `CI=true dotnet build wintty.sln -c Release` |
| Run all tests | `dotnet test tests/Wintty.Tests -c Release` |
| Tests with the coverage gate | `dotnet test tests/Wintty.Tests -c Release -p:CollectCoverage=true -p:Threshold=90 -p:ThresholdType=line -p:ThresholdStat=total` |
| Build the docs site | `cd docs && npm install && npm run build` |
| Set a version | `-p:Version=1.2.3` (releases take it from the git tag; the default is `0.0.0-dev`) |

Running the app: `dotnet build src/Wintty.App -c Release` writes to `src/Wintty.App/bin/Release/net10.0-windows10.0.19041.0/win-x64/`. A stale copy may exist under `bin/x64/`; check file timestamps before launching an exe. Startup errors are shown in the window and logged to `%LOCALAPPDATA%\wintty\wintty.log`.

## Architecture

Dependencies point one way: `Wintty.App` -> `Wintty.Render` and `Wintty.Ghostty` -> `Wintty.Core`.

- `Wintty.Core` is pure .NET with no native or UI dependencies. It holds the abstractions (`ITerminalEngine`, `ITerminalEngineFactory`, `IPtyConnection`, `IPtyConnectionFactory`), `TerminalSnapshot` (an immutable frame of resolved-RGB cells and the cursor) and `TerminalSession` (pumps process output into the engine).
- `Wintty.Ghostty` is the **only** project that may reference `LibGhostty.Net` or libghostty. `GhosttyGateway` implements the Core factories, including ConPTY process hosting. Architecture tests fail the build if any other project references the package.
- `Wintty.Render` is pure .NET: `RenderPlanner` turns a snapshot into a draw plan and `GridSizer` computes the cell grid. It has no graphics API dependency.
- `Wintty.App` is the thin WinUI 3 shell. `TerminalControl` draws a `RenderPlan` with Win2D. Keep logic out of it so it stays testable elsewhere.

When adding behaviour, put it in the lowest layer that can own it and expose it through an interface in Core. Prefer small interfaces and composition over inheritance.

## Rules

- **Commits and PR titles use Conventional Commits** (`feat(render): ...`, `fix: ...`, `docs: ...`). PRs are squash-merged and the PR title becomes the commit, so the title must be conventional.
- **Never push to `main`.** It is protected by a ruleset: PRs only, squash merges only, one approval with code-owner review (the repo admin may merge through a PR), linear history, signed commits, and required checks `build`, `docs`, `commits` and `dependency-review` plus a CodeQL gate. Work on a branch and open a PR.
- **Do not merge a PR unless the user asked you to.** Do not enable auto-merge.
- **Every public type and member in the library projects needs XML docs.** Missing docs fail the CI build (`WinUI` app and tests are exempt).
- **Tests are mandatory for new code.** The goal is full coverage and CI fails below 90% line coverage; raise the gate as coverage improves and never lower it. Use the fakes in `tests/Wintty.Tests/Fakes`. Tests that load the real native library carry `[Trait("Category", "Integration")]` and `[Collection("NativeEnvironment")]` so they run serially.
- **Pin third-party GitHub Actions to a commit SHA** with the tag in a trailing comment. Dependabot updates them.
- **Target .NET 10.** C# union types arrive with .NET 11; until then model closed sets as sealed record hierarchies.
- **Never commit generated output:** `artifacts/`, `coverage.json`, `bin/`, `obj/`. Line endings are LF (`.gitattributes`).
- **Do not rename or add a dependency on `LibGhostty.Net` anywhere except `Wintty.Ghostty`.**

## Gotchas

- `LibGhostty.Net` does not find its native files in non-RID-specific builds. `NativeAssetLocator` (in `Wintty.Ghostty`) sets `LIBGHOSTTY_NATIVE_DIR` when it is unset. Do not remove it.
- `IGhosttyPtyConnection.Kill()` throws if the process already exited. `GhosttyPtyConnection.Kill` tolerates that; keep it tolerant.
- Win2D: measure text with `CanvasDevice.GetSharedDevice()`, not the control, because `Loaded` fires before the control has a device.
- Windows PowerShell 5 cannot load net10 assemblies. For reflection or quick probes use a throwaway `dotnet` console project outside the repo.
- Screenshots: capture only the wintty window (`PrintWindow`), never the whole desktop, which can include unrelated private content.
- In Git Bash on Windows, long heredocs containing apostrophes can fail to parse. Write files with the editor tool instead.
- Releases are cut by pushing a `vX.Y.Z` tag, which only the repo admin can create. Do not create or move tags.

## Where things live

- `src/` projects, `tests/Wintty.Tests` (xUnit), `docs/` (Astro Starlight site, published to GitHub Pages), `scripts/` (release helpers), `.github/` (workflows, CODEOWNERS, templates).
- Work items are tracked as GitHub issues. The `tasks/` folder holds legacy specs until they are migrated.
- Human contributor guidance is in `CONTRIBUTING.md`; this file is for agents.
