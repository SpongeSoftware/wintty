# 14 Architecture and quality gates

Status: partly done (abstractions, architecture tests, 90% line-coverage gate in CI; XML-doc enforcement active; ratchet toward 100% ongoing)

**Goal:** enforce the engineering standards in code and CI.

**Scope:**
- Core abstractions in `Wintty.Core`: `ITerminalEngine`, `ITerminalEngineFactory`, `IPtyHost`, `IRenderSurface`, `IClock`/`IFileSystem` where needed. Concrete native implementations live in `Wintty.Ghostty` (the gateway) and `Wintty.Render`.
- **Single libghostty gateway:** the `Wintty.Ghostty` project (`GhosttyGateway`) is the only code that references libghostty-vt or `LibGhostty.Net`. All other code depends on `ITerminalEngine`.
- Architecture tests (`NetArchTest` or reflection-based): only the gateway assembly/namespace references the native binding; `Core` has no Windows or UI references; dependency direction `App -> Render/Ghostty -> Core`.
- Coverage: `coverlet` + report in CI, threshold gate starting at the current level and ratcheting to 100% of testable code; native/GPU glue excluded via `[ExcludeFromCodeCoverage]` with a justification, and covered by tagged integration tests instead.
- Docs: XML docs required on libraries (CS1591 is an error in CI); keep `docs/` Engineering and Architecture pages current.
- Replace the sealed-record "closed hierarchy" pattern with C# `union` types after .NET 11 ships.

**Acceptance:** CI fails on a stray libghostty reference outside the gateway, on missing XML docs, and on coverage below the threshold; fakes let `Core` logic be tested with no native code.

**Files:** `src/Wintty.Core/Abstractions/*`, `tests/Wintty.Tests/Architecture/*`, `.github/workflows/ci.yml`, `Directory.Build.props`.
