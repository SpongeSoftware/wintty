# wintty

A native Windows terminal emulator. It shows the screen of a shell running behind a pseudo-console, using Ghostty's terminal core for parsing and screen state.

## Terminal

**Terminal engine**:
The component that consumes a program's output bytes and keeps the resulting screen, scrollback and modes. In wintty this is libghostty-vt behind the engine interface.
_Avoid_: Emulator core, parser, VT

**Gateway**:
The single project allowed to touch libghostty. It supplies the engine, key encoder and process hosting to every other project.
_Avoid_: Wrapper, binding, adapter

**Session**:
One running shell together with the engine that tracks its screen. A tab or split pane shows exactly one session.
_Avoid_: Terminal, connection, process

**PTY connection**:
The pseudo-console link to a session's shell process: output bytes in, input bytes and resize requests out.
_Avoid_: Pipe, ConPTY (that is the Windows mechanism, not our term)

**Snapshot**:
An immutable picture of the visible screen at one moment: cells with resolved colours, plus the cursor.
_Avoid_: Frame, buffer, screen state

**Cell**:
One grid position holding a character cluster, its colours and its style.
_Avoid_: Glyph (a glyph is how a cell is drawn)

**Scrollback**:
The lines that have moved off the top of the screen and can still be scrolled back to.
_Avoid_: History

## Rendering

**Render plan**:
The list of things to draw for one snapshot, computed without any graphics API. The window draws a plan; it does not interpret cells itself.
_Avoid_: Draw list, scene

**Grid size**:
The number of columns and rows that fit a pixel area at a given cell size.
_Avoid_: Terminal size, dimensions

**Symbol font**:
An installed font used to draw private-use icon glyphs that the main font lacks, such as Nerd Font icons.
_Avoid_: Fallback font, icon font

## Colour

**Colour scheme**:
The default foreground, background, cursor colour and palette overrides a terminal draws with. Anything unset keeps the engine's built-in value.
_Avoid_: Theme (a theme is a named, bundled scheme)

**Theme**:
A named colour scheme, built in or kept as a file in the user's themes folder. A user file wins over a built-in of the same name.
_Avoid_: Skin, colour profile

**Palette**:
The 256 indexed colours; indexes 0 to 15 are the ANSI colours.
_Avoid_: Colour table

## Configuration

**Config**:
The user's Ghostty-style `key = value` file. A later line wins, except palette lines, which accumulate. An empty value resets the option.
_Avoid_: Settings, preferences

**Diagnostic**:
A line-numbered message about a config line that was skipped or could not be applied. It never stops the rest of the file from loading.
_Avoid_: Error, warning

**Live option**:
A config option that takes effect on a running terminal when the file is saved, as opposed to one that applies only to new terminals.
_Avoid_: Hot setting

## Input

**Key encoding**:
Turning a physical key press and its modifiers into the bytes a program expects, following the terminal modes the program has set.
_Avoid_: Key mapping (that is the Windows key to Ghostty key step only)

## Project

**Kitty compatibility**:
The goal of supporting the Kitty protocol extensions (keyboard, graphics and others), tracked as the K1 to K8 issues.
_Avoid_: Kitty mode

**Portable zip**:
The release artifact that runs without installing, built from a version tag.
_Avoid_: Standalone build
