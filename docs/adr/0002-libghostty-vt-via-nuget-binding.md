# Use libghostty-vt through the LibGhostty.Net package

wintty takes Ghostty's terminal core as the prebuilt native library shipped in the `LibGhostty.Net` NuGet package, rather than building libghostty from source with Zig or writing its own parser. This avoids a native toolchain in CI and for contributors, and gives Ghostty's behaviour for free. The cost is that we follow the package's version and platforms: Windows x64 only, with arm64 deferred until its natives ship arm64 binaries.

## Consequences

The library lacks some features we want (for example answers to OSC colour queries and DECRQSS, tracked in #40), so some behaviour has to be built on top or wait for upstream.
