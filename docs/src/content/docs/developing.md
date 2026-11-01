---
title: Developing wintty
description: Build, test, debug and release wintty.
---

## Build and test

You need Windows 10 (19041) or newer, the .NET 10 SDK pinned in `global.json`, and Git. Run these from the repository root.

```bash
dotnet build wintty.sln -c Release
dotnet test tests/Wintty.Tests -c Release
```

CI builds with warnings as errors; reproduce that with `CI=true dotnet build wintty.sln -c Release`. Coverage is gated at 90% of lines:

```bash
dotnet test tests/Wintty.Tests -c Release -p:CollectCoverage=true -p:Threshold=90 -p:ThresholdType=line -p:ThresholdStat=total
```

Tests that load the real native library carry `[Trait("Category", "Integration")]`; run only the fast ones with `--filter "Category!=Integration"`. Unit tests use the fakes in `tests/Wintty.Tests/Fakes`.

The docs site needs Node 22.23.4 or newer: `cd docs && npm install && npm run build`.

## Run and debug the app

For daily use, and to try features as they land, run `pwsh scripts/run-dev.ps1`. It publishes a self-contained build to `artifacts/wintty-dev` and starts it. `-Pull` fast-forwards `main` first, `-NoBuild` starts the last build, and `-Log` follows `%LOCALAPPDATA%\wintty\wintty.log` afterwards. A running dev build is closed before it is replaced.


`dotnet build src/Wintty.App -c Release` writes the app to `src/Wintty.App/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/`. Startup errors are shown in the window and logged to `%LOCALAPPDATA%\wintty\wintty.log`. A stale build may exist outside the `x64` folder, so check file timestamps before launching.

Open `wintty.sln` in Visual Studio 2022 and set `Wintty.App` as the startup project to debug. For the gateway, start with the integration tests: they exercise libghostty and ConPTY without the UI.

## Releases

Only the repository admin releases, by pushing a tag `vX.Y.Z`. The tag is the only place the version lives. The `Release` workflow then:

1. builds, tests and publishes a self-contained x64 build with the version taken from the tag, and zips it;
2. writes SHA-256 checksums and generates SPDX and CycloneDX SBOMs with Syft;
3. generates release notes and the changelog from Conventional Commits with git-cliff;
4. generates winget manifests;
5. publishes a GitHub Release with the zip, checksums, SBOMs and manifests (a tag with `-rc` or `-beta` is a prerelease).

The documentation site rebuilds when a release is published, so the Download page shows the new version.

## How agent instructions are organised

`AGENTS.md` at the repository root is the source of truth for AI coding agents and is tool-agnostic; `CLAUDE.md` imports it. `GLOSSARY.md` and `docs/adr/` hold the vocabulary and decisions; `docs/agents/` configures the issue tracker and triage labels. Work is tracked as GitHub issues.
