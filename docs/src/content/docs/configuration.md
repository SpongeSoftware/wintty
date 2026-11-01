---
title: Configuration
description: wintty configuration reference.
---

wintty reads `%APPDATA%\wintty\config`. The file does not exist until you create it; without it every option has its default. Saving the file applies changes to a running window, so you can tune the font and see the result immediately.

## New tabs and splits start in the current directory

If your shell reports its directory, a new tab or split opens where you were. Shells report it with the OSC 7 escape sequence; wintty follows Windows paths (`file://host/C:/Users/me`). Paths that only exist inside a WSL distribution cannot be used as a Windows directory, so tabs opened from such a pane start in the default directory.

- **PowerShell 7:** add this to your profile (`$PROFILE`):

  ```powershell
  function prompt {
      $cwd = $executionContext.SessionState.Path.CurrentLocation
      $uri = ([System.Uri]::new($cwd.ProviderPath)).AbsoluteUri
      "$([char]27)]7;$uri$([char]7)PS $cwd> "
  }
  ```

- **cmd:** set `PROMPT` to include the sequence: `prompt $E]7;file://localhost/$P$E\$P$G` (`$E` is the escape character, `$P` the current directory).
- **Git Bash:** add this to `~/.bashrc`: `PROMPT_COMMAND='printf "\e]7;file://%s/%s" "$HOSTNAME" "$(cygpath -m "$PWD")"'`. Plain bash inside WSL reports Linux paths, which wintty cannot use as a Windows directory (see above).

## File format

The format follows Ghostty's `key = value` style.

- One setting per line. Whitespace around the key and value is ignored, and keys are case-insensitive.
- Lines starting with `#` are comments. A comment must be on its own line.
- Wrap a value in double quotes if you like: `font-family = "Cascadia Mono"`.
- If a key appears more than once, the last one wins.
- An empty value resets the option to its default: `font-size =`.
- A line that cannot be applied (unknown option, bad value, missing `=`) is reported and skipped. The other lines still take effect.

Problems are listed in a red banner along the bottom of the window and written to `%LOCALAPPDATA%\wintty\wintty.log`. The banner disappears when the file is fixed and saved.

```ini
# %APPDATA%\wintty\config
theme = catppuccin-mocha
palette = 1=#ff5555
font-family = Cascadia Mono
font-size = 14
window-padding = 8
cursor-style-blink = false
scrollback-limit = 20000
command = pwsh.exe -NoLogo
```

## Options

This table is generated from the option list in the code (`ConfigOptions`), so it always matches what the app accepts.

<!-- BEGIN GENERATED OPTIONS -->
| Option | Default | Applies | Description |
| --- | --- | --- | --- |
| `font-family` | `Cascadia Mono` | live | Font for terminal text. Private-use symbols still fall back to an installed Nerd Font. |
| `font-size` | `15` | live | Size in device-independent pixels, from 4 to 200. Fractions are allowed. |
| `window-padding` | `6` | live | Space in pixels between the window edge and the text grid, on every side. 0 to 200. |
| `cursor-style-blink` | `true` | live | `true` or `false`. Whether the cursor blinks. |
| `clipboard-paste-protection` | `true` | live | `true` or `false`. Ask before pasting text that contains line breaks into a program that has not enabled bracketed paste. |
| `keybind` | see [Keybindings](/wintty/keybindings/) | live | One binding per line as `trigger=action`, for example `keybind = ctrl+shift+n=new_tab`. Repeat the line for more. `unbind` as the action removes a default; the value `clear` drops all defaults; an empty value forgets every `keybind` line above it. |
| `theme` | none | live | Colour theme by name, for example `catppuccin-mocha` or `Gruvbox Dark`. See [Themes](/wintty/themes/). Your own colour options below are layered on top of the theme. |
| `foreground` | theme or built-in | live | Default text colour as `#rrggbb` (or `#rgb`). |
| `background` | theme or built-in | live | Default background colour. |
| `cursor-color` | theme or text colour | live | Cursor colour. |
| `palette` | theme or built-in | live | One palette entry per line as `index=#rrggbb`, index 0 to 255. Repeat the line for more entries. An empty value clears every `palette` line above it. |
| `scrollback-limit` | `10000` | new terminals | Number of scrollback lines kept. Zero disables scrollback. |
| `command` | auto-detected | new terminals | Program to run, with arguments. Quote a path that contains spaces: `"C:\Program Files\PowerShell\7\pwsh.exe" -NoLogo`. When unset wintty runs `pwsh.exe` if it is on `PATH`, otherwise `powershell.exe`. |
<!-- END GENERATED OPTIONS -->

"Live" options change in the running window when the file is saved. "New terminals" options are read when a terminal starts, so they take effect the next time you open one.

:::note
Selection colours and cursor style are not read yet. They will be added to this table as they are implemented. An option wintty does not know is reported as a problem rather than silently ignored.

Editing a theme file in `%APPDATA%\wintty\themes` is picked up the next time the config file is saved.
:::
