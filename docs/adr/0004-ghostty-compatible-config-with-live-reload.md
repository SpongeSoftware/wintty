# Ghostty-compatible config file, reloaded on save

The config uses Ghostty's `key = value` format and option names where they apply, so existing Ghostty users and their themes carry over (the built-in themes are Ghostty's set). Bad lines produce line-numbered diagnostics and are skipped, never a startup failure, and saving the file applies live options to open terminals. A theme is the base and the file's own colour options layer on top regardless of line order, matching Ghostty. Options Ghostty has that wintty does not support yet are reported as unknown rather than silently ignored.
