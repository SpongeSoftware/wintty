# 03 CI workflow

Status: todo (draft in `.github/workflows/ci.yml`)

**Goal:** PR/push validation.

**Scope:** commitlint on PR commits and title, dotnet build+test, docs build, caching.

**Acceptance:** workflow passes on a branch once pushed to GitHub; lint fails for a non-conventional PR title. Validate locally with `act` where possible.

**Files:** `.github/workflows/ci.yml`.
