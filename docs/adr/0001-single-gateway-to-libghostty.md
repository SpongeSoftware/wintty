# Only one project may reference libghostty

`Wintty.Ghostty` is the only project that references `LibGhostty.Net` or libghostty; everything else programs against interfaces in `Wintty.Core`. The native API is young and changes between versions, so confining it to one project keeps an upgrade or a swap of the binding to a single place, and lets the rest of the code be tested with fakes. Architecture tests fail the build if another project takes the dependency.

## Considered options

Letting the app call the binding directly was simpler at first, but spread native types and lifetimes through the UI and made most code untestable without the native library.
