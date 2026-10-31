# 04 Release workflow

Status: todo (draft in `.github/workflows/release.yml`)

**Goal:** tag `vX.Y.Z` produces a GitHub Release.

**Scope:** x64/arm64 self-contained publish, portable zips, SHA256SUMS, Syft SBOMs (SPDX + CycloneDX) per arch, git-cliff release notes and CHANGELOG, winget manifests (`scripts/make-winget-manifest.py`), prerelease flag for tags with a suffix, version injected from tag.

**Acceptance:**
- Test tag on a scratch repo yields release with zips, SBOMs, checksums, manifests and notes.
- `winget validate out/winget` passes.
- Decide whether CHANGELOG.md is committed back to `main` (needs a PR/bot) or only attached; default: attached and pasted into notes.
- Fix actual paths for SBOM scan (publish dir layout) once the app project exists.

**Files:** `.github/workflows/release.yml`, `cliff.toml`, `scripts/`.
