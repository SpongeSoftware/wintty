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

## Engineering standards

- **Tests:** aim for full coverage; new code ships with tests. Coverage is gated in CI.
- **Docs:** every public type/member has XML docs (enforced for libraries).
- **Layering:** `App` -> `Render`/`Pty` -> `Core`. Small interfaces, composition over inheritance.
- **libghostty:** only the single gateway in `Wintty.Core` may reference libghostty-vt or its NuGet package.

See the docs site "Engineering standards" page for details.

## .NET version and C# unions

wintty targets .NET 10 (LTS). C# 15 union types ship with .NET 11 (preview at the time of writing). Migration plan once .NET 11 is released: bump `global.json`, set `net11.0` TFMs and the `setup-dotnet` version, then introduce `union` types for closed sets (for example input events, config values, parse results). Until then, model such sets as sealed record hierarchies with a private constructor on the base, so call sites can switch to unions with minimal change.

Add yourself to [CONTRIBUTORS.md](CONTRIBUTORS.md) in your first PR.
