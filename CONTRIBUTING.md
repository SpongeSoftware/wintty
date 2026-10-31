# Contributing

Thanks for helping with wintty.

## Commits

All commits and PR titles must follow [Conventional Commits](https://www.conventionalcommits.org):

```
feat(render): add glyph atlas
fix(pty): debounce resize
docs: update install page
```

Allowed types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. Use `!` or a `BREAKING CHANGE:` footer for breaking changes.

Install the local hook once with `npm install` in the repo root (husky + commitlint).

## Releases

Maintainers release by pushing a tag `vX.Y.Z`. The version is taken from the tag; do not edit version numbers in project files. The changelog and release notes are generated from commit messages.

## Development

1. Fork and branch from `main`.
2. `dotnet build` and `dotnet test` must pass.
3. Open a PR with a conventional title.

Add yourself to [CONTRIBUTORS.md](CONTRIBUTORS.md) in your first PR.
