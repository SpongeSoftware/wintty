# 05 Docs site (Astro Starlight) and Pages

Status: in-progress (skeleton in `docs/`)

**Goal:** static docs served from GitHub Pages with a Download page for the latest release.

**Scope:** `npm install && npm run build` in `docs/`; verify base path under `/<repo>`; Download page lists latest release assets (build-time fetch, rebuilt by `docs.yml` on release published); flesh out pages; add screenshots later.

**Acceptance:** build succeeds locally and in CI; links work under the Pages base path; Download page shows placeholder with no releases and assets after one exists. Enable Pages (Source: GitHub Actions) when the repo is on GitHub.

**Files:** `docs/**`, `.github/workflows/docs.yml`.
