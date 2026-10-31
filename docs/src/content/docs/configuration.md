---
title: Configuration
description: wintty configuration reference.
---

wintty reads `%APPDATA%\wintty\config`. The file does not exist until you create it; without it every option has its default. Saving the file applies changes to a running window, so you can tune the font and see the result immediately.

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
font-family = Cascadia Mono
font-size = 14
window-padding = 8
cursor-style-blink = false
scrollback-limit = 20000
command = pwsh.exe -NoLogo
```

## Options

| Option | Default | Applies | Description |
| --- | --- | --- | --- |
| `font-family` | `Cascadia Mono` | live | Font for terminal text. Private-use symbols still fall back to an installed Nerd Font. |
| `font-size` | `15` | live | Size in device-independent pixels, from 4 to 200. Fractions are allowed. |
| `window-padding` | `6` | live | Space in pixels between the window edge and the text grid, on every side. 0 to 200. |
| `cursor-style-blink` | `true` | live | `true` or `false`. Whether the cursor blinks. |
| `scrollback-limit` | `10000` | new terminals | Number of scrollback lines kept. Zero disables scrollback. |
| `command` | auto-detected | new terminals | Program to run, with arguments. Quote a path that contains spaces: `"C:\Program Files\PowerShell\7\pwsh.exe" -NoLogo`. When unset wintty runs `pwsh.exe` if it is on `PATH`, otherwise `powershell.exe`. |

"Live" options change in the running window when the file is saved. "New terminals" options are read when a terminal starts, so they take effect the next time you open one.

:::note
Colours, themes, palette entries and cursor style are on the roadmap and are not read yet. They will be added to this table as they are implemented. An option wintty does not know is reported as a problem rather than silently ignored.
:::
