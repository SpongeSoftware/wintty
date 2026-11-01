# Releases come from version tags; main only changes through squash-merged PRs

The version is never committed. Pushing a `vX.Y.Z` tag, which only the repo admin can do, builds, tests and publishes the release (portable zip, SBOM, release notes from Conventional Commits, winget manifest). `main` is protected: PRs only, squash merges, signed commits and required checks. Because a squash merge turns the PR title into the commit, titles must be Conventional Commits, which is what the changelog and release notes are generated from.
