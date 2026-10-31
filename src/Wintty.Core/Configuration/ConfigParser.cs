using System.Globalization;
using Wintty.Core.Abstractions;

namespace Wintty.Core.Configuration;

/// <summary>
/// Parses Ghostty-style <c>key = value</c> configuration text. Lines starting with <c>#</c> are comments, values
/// may be wrapped in double quotes, a repeated key replaces the earlier value, and an empty value resets the
/// option to its default. Bad lines are reported and skipped; they never discard the valid ones.
/// </summary>
public static class ConfigParser
{
    /// <summary>Parses configuration text.</summary>
    /// <param name="text">The file contents.</param>
    /// <returns>The settings and any diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static ConfigLoadResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var config = new WinttyConfig();
        var diagnostics = new List<ConfigDiagnostic>();
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var number = index + 1;
            var line = lines[index].Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                diagnostics.Add(new ConfigDiagnostic(number, $"expected 'key = value' but found '{line}'"));
                continue;
            }

            var key = line[..equals].Trim().ToLowerInvariant();
            var value = Unquote(line[(equals + 1)..].Trim());
            config = Apply(config, key, value, out var error);
            if (error is not null)
            {
                diagnostics.Add(new ConfigDiagnostic(number, error));
            }
        }

        return new ConfigLoadResult(config, diagnostics);
    }

    /// <summary>Parses a colour written as <c>#rrggbb</c>, <c>rrggbb</c>, <c>#rgb</c> or <c>rgb</c>.</summary>
    /// <param name="text">The colour text.</param>
    /// <param name="color">The colour when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the text is a valid colour.</returns>
    public static bool TryParseColor(string text, out TerminalColor color)
    {
        color = default;
        var hex = text.StartsWith('#') ? text[1..] : text;
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = new TerminalColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    // Returns the updated settings; error is set (and the settings unchanged) when the line is rejected.
    private static WinttyConfig Apply(WinttyConfig config, string key, string value, out string? error)
    {
        var defaults = new WinttyConfig();
        error = null;
        switch (key)
        {
            case "font-family":
                return config with { FontFamily = value.Length == 0 ? defaults.FontFamily : value };
            case "font-size":
                return TryNumber(value, defaults.FontSize, 4f, 200f, key, out var size, out error)
                    ? config with { FontSize = size } : config;
            case "window-padding":
                return TryNumber(value, (float)defaults.WindowPadding, 0f, 200f, key, out var padding, out error)
                    ? config with { WindowPadding = padding } : config;
            case "scrollback-limit":
                return TryWhole(value, defaults.ScrollbackLimit, key, out var scrollback, out error)
                    ? config with { ScrollbackLimit = scrollback } : config;
            case "cursor-style-blink":
                return TryFlag(value, defaults.CursorBlink, key, out var blink, out error)
                    ? config with { CursorBlink = blink } : config;
            case "command":
                return config with { Command = value.Length == 0 ? null : value };
            default:
                error = $"unknown option '{key}'";
                return config;
        }
    }

    private static bool TryNumber(string value, float fallback, float min, float max, string key, out float result, out string? error)
    {
        error = null;
        result = fallback;
        if (value.Length == 0)
        {
            return true;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && !float.IsNaN(number) && number >= min && number <= max)
        {
            result = number;
            return true;
        }

        error = $"{key} must be a number between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}, not '{value}'";
        return false;
    }

    private static bool TryWhole(string value, int fallback, string key, out int result, out string? error)
    {
        error = null;
        result = fallback;
        if (value.Length == 0)
        {
            return true;
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            result = number;
            return true;
        }

        error = $"{key} must be a whole number of zero or more, not '{value}'";
        return false;
    }

    private static bool TryFlag(string value, bool fallback, string key, out bool result, out string? error)
    {
        error = null;
        result = fallback;
        switch (value.ToLowerInvariant())
        {
            case "":
                return true;
            case "true":
                result = true;
                return true;
            case "false":
                result = false;
                return true;
            default:
                error = $"{key} must be true or false, not '{value}'";
                return false;
        }
    }
}
