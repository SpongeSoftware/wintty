---
title: Engineering standards
description: Testing, documentation, layering and dependency rules for wintty.
---

## Testing

- The goal is **full test coverage**. Coverage is collected with `coverlet` and enforced in CI with a line-coverage threshold (currently 90%) that only ratchets upward.
- Logic lives in testable libraries (`Wintty.Core`, `Wintty.Ghostty`, `Wintty.Render`). `Wintty.App` stays thin (views and wiring only).
- Anything that touches Windows, the GPU, ConPTY or the native library sits behind an interface so it can be faked in unit tests; real integration tests are tagged separately.

## Documentation

- Every public type and member has XML doc comments (`GenerateDocumentationFile` is on; missing docs are build errors in CI).
- Architectural decisions are recorded under `docs/` and in `tasks/`.

## Layering and abstraction

- Dependencies point inward: `App` -> `Render`/`Ghostty` -> `Core` (abstractions + domain). `Core` has no Windows or native dependencies.
- Prefer composition and small interfaces. Use base classes only for genuine shared behaviour (for example a common session base), never to share unrelated helpers.

## One entry point to libghostty

All access to libghostty-vt (or the `LibGhostty.Net` NuGet package) goes through a single gateway in the `Wintty.Ghostty` project: `GhosttyGateway` implements the `ITerminalEngineFactory` and `IPtyConnectionFactory` abstractions from `Wintty.Core`. No other project references the native library or the NuGet package, and an architecture test fails the build if one does. This keeps the dependency swappable and the rest of the code unit-testable with a fake engine.

## Language and runtime

wintty targets **.NET 10** (LTS). C# union types arrive with C# 15 / .NET 11; we will adopt them after .NET 11 ships (see the migration notes in the repository `CONTRIBUTING.md`).
