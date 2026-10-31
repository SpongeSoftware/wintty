# wintty

A native Windows terminal emulator built on [libghostty-vt](https://github.com/ghostty-org/ghostty), the terminal core extracted from Ghostty. Written in C# with WinUI 3.

> **Status:** early development. Nothing is released yet.

wintty is an independent project, not affiliated with the Ghostty project.

## Goals

- Ghostty's terminal behaviour (VT parsing, scrollback, key encoding) via libghostty-vt
- ConPTY-backed shells: PowerShell, cmd, WSL
- GPU-accelerated rendering (Direct3D 11 + DirectWrite)
- Tabs and splits
- Ghostty-style `key = value` configuration with hot reload

## Install

Downloads are published on the [GitHub Releases](../../releases) page and on the documentation site. Each release includes a portable x64 zip, SPDX/CycloneDX SBOMs, checksums and a winget manifest.

## Build

Requirements: Windows 11, .NET SDK 10+, Windows App SDK, Visual Studio 2022 (or Build Tools), Zig (to build libghostty-vt).

```powershell
dotnet build wintty.sln
```

See [CONTRIBUTING.md](CONTRIBUTING.md) and the [`tasks/`](tasks) directory for the roadmap.

## Versioning and commits

- [Semantic Versioning](https://semver.org). The version comes from the git tag (`vX.Y.Z`) at release time.
- [Conventional Commits](https://www.conventionalcommits.org) are required. The changelog is generated from them with git-cliff.

## License

[MIT](LICENSE). See [NOTICE](NOTICE) for third-party attributions.
