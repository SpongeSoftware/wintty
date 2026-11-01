# Draw from a pure render plan, not directly from the engine

Each frame goes engine -> snapshot -> render plan -> Win2D drawing. The snapshot is immutable with resolved RGB colours, and the render plan is computed in pure .NET with no graphics dependency, so layout, colour and cursor logic are unit tested without a window or GPU. The WinUI control only draws a plan. Win2D was chosen over hand-written Direct3D for text drawing and glyph handling at far lower cost; if profiling shows it is too slow, the plan boundary lets the drawing layer be replaced alone.

## Considered options

Reading cells straight from the engine inside the control was less code but put locking, colour resolution and layout in untestable UI code.
