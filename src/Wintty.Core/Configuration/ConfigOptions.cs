using System.Globalization;
using System.Text;

namespace Wintty.Core.Configuration;

/// <summary>Describes one option of the config file, for the generated reference documentation.</summary>
/// <param name="Name">The key as written in the file.</param>
/// <param name="Default">The value used when the option is not set, as Markdown (a literal value is in backticks).</param>
/// <param name="Live">
/// <see langword="true"/> when saving the file changes a running window; <see langword="false"/> when the option is
/// only read as a terminal starts.
/// </param>
/// <param name="Description">What the option does and which values it accepts, in Markdown.</param>
public sealed record ConfigOption(string Name, string Default, bool Live, string Description);

/// <summary>
/// The documented options of the config file. The reference page on the documentation site is rendered from this
/// list, and tests fail when it and <see cref="ConfigParser"/> disagree or when the page is out of date.
/// </summary>
public static class ConfigOptions
{
    private static readonly WinttyConfig Defaults = new();

    /// <summary>Every option the parser understands, in the order the reference lists them.</summary>
    public static IReadOnlyList<ConfigOption> All { get; } =
    [
        new("font-family", Code(Defaults.FontFamily), true,
            "Font for terminal text. Private-use symbols still fall back to an installed Nerd Font."),
        new("font-size", Code(Defaults.FontSize), true,
            "Size in device-independent pixels, from 4 to 200. Fractions are allowed."),
        new("window-padding", Code(Defaults.WindowPadding), true,
            "Space in pixels between the window edge and the text grid, on every side. 0 to 200."),
        new("cursor-style-blink", Code(Defaults.CursorBlink ? "true" : "false"), true,
            "`true` or `false`. Whether the cursor blinks."),
        new("theme", "none", true,
            "Colour theme by name, for example `catppuccin-mocha` or `Gruvbox Dark`. See [Themes](/wintty/themes/). " +
            "Your own colour options below are layered on top of the theme."),
        new("foreground", "theme or built-in", true, "Default text colour as `#rrggbb` (or `#rgb`)."),
        new("background", "theme or built-in", true, "Default background colour."),
        new("cursor-color", "theme or text colour", true, "Cursor colour."),
        new("palette", "theme or built-in", true,
            "One palette entry per line as `index=#rrggbb`, index 0 to 255. Repeat the line for more entries. " +
            "An empty value clears every `palette` line above it."),
        new("scrollback-limit", Code(Defaults.ScrollbackLimit), false,
            "Number of scrollback lines kept. Zero disables scrollback."),
        new("command", "auto-detected", false,
            "Program to run, with arguments. Quote a path that contains spaces: " +
            "`\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\" -NoLogo`. When unset wintty runs `pwsh.exe` if it is on " +
            "`PATH`, otherwise `powershell.exe`."),
    ];

    /// <summary>Renders the options as a Markdown table, the form used on the reference page.</summary>
    /// <returns>The table, ending with a newline.</returns>
    public static string RenderTable()
    {
        var builder = new StringBuilder();
        builder.Append("| Option | Default | Applies | Description |\n| --- | --- | --- | --- |\n");
        foreach (var option in All)
        {
            builder.Append(CultureInfo.InvariantCulture, $"| `{option.Name}` | {option.Default} | {(option.Live ? "live" : "new terminals")} | {option.Description} |\n");
        }

        return builder.ToString();
    }

    private static string Code(string value) => "`" + value + "`";

    private static string Code(IFormattable value) => Code(value.ToString(null, CultureInfo.InvariantCulture));
}
