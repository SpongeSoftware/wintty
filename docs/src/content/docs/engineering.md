---
title: Engineering standards
description: Testing, documentation, layering and dependency rules for wintty.
---

## Testing

- The goal is **full test coverage**. Coverage is collected with `coverlet` and enforced in CI with a line-coverage threshold (currently 95%) that only ratchets upward.
- Logic lives in testable libraries (`Wintty.Core`, `Wintty.Ghostty`, `Wintty.Render`). `Wintty.App` stays thin (views and wiring only).
- Anything that touches Windows, the GPU, ConPTY or the native library sits behind an interface so it can be faked in unit tests; real integration tests are tagged separately.

## Documentation

- Every public type and member has XML doc comments (`GenerateDocumentationFile` is on; missing docs are build errors in CI).
- Architectural decisions are recorded under `docs/` (ADRs) and discussed in GitHub issues.

## Layering and abstraction

- Dependencies point inward: `App` -> `Render`/`Ghostty` -> `Core` (abstractions + domain). `Core` has no Windows or native dependencies.
- Prefer composition and small interfaces. Use base classes only for genuine shared behaviour (for example a common session base), never to share unrelated helpers.

## One entry point to libghostty

All access to libghostty-vt (or the `LibGhostty.Net` NuGet package) goes through a single gateway in the `Wintty.Ghostty` project: `GhosttyGateway` implements the `ITerminalEngineFactory` and `IPtyConnectionFactory` abstractions from `Wintty.Core`. No other project references the native library or the NuGet package, and an architecture test fails the build if one does. This keeps the dependency swappable and the rest of the code unit-testable with a fake engine.

## Language and runtime

wintty targets **.NET 10** (LTS). C# union types arrive with C# 15 / .NET 11; we will adopt them after .NET 11 ships (see the migration notes in the repository `CONTRIBUTING.md`).

## Coverage scope and exclusions

The gate measures the assemblies the test project references: `Wintty.Core`, `Wintty.Ghostty`, `Wintty.Render` and `Wintty.Input`. `Wintty.App` (the WinUI shell: `TerminalControl`, `MainWindow`, native keyboard calls) is deliberately outside it because it needs a window and a GPU. Logic that can be tested without them belongs in the libraries; the shell only wires it up. Selection, scrolling, mouse encoding, resize debouncing and hit testing already live there. What remains in the shell is pointer, keyboard and clipboard glue, the blink timer, title handling and startup error reporting, which is checked by hand against the real window.

Within the measured assemblies nothing is excluded from coverage.
