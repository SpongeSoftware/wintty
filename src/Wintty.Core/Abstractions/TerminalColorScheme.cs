using System.Collections.Frozen;

namespace Wintty.Core.Abstractions;

/// <summary>
/// The colours a terminal draws with. Anything left unset keeps the terminal's built-in value, so a scheme can
/// be as small as a single palette entry.
/// </summary>
/// <param name="Foreground">Default text colour, or <see langword="null"/> for the built-in one.</param>
/// <param name="Background">Default background colour, or <see langword="null"/> for the built-in one.</param>
/// <param name="CursorColor">Cursor colour, or <see langword="null"/> to draw the cursor in the text colour.</param>
/// <param name="Palette">Overrides for 256-colour palette entries, keyed by index 0 to 255.</param>
public sealed record TerminalColorScheme(
    TerminalColor? Foreground = null,
    TerminalColor? Background = null,
    TerminalColor? CursorColor = null,
    IReadOnlyDictionary<int, TerminalColor>? Palette = null)
{
    /// <summary>The number of entries in the terminal palette.</summary>
    public const int PaletteSize = 256;

    /// <summary>A scheme that changes nothing.</summary>
    public static TerminalColorScheme Default { get; } = new();

    /// <summary>The palette overrides; empty when there are none.</summary>
    public IReadOnlyDictionary<int, TerminalColor> Entries { get; } = Palette ?? FrozenDictionary<int, TerminalColor>.Empty;

    /// <summary>
    /// Layers another scheme on top of this one: every colour <paramref name="top"/> sets wins, everything it
    /// leaves unset keeps this scheme's value.
    /// </summary>
    /// <param name="top">The scheme whose values take priority.</param>
    /// <returns>The combined scheme.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="top"/> is <see langword="null"/>.</exception>
    public TerminalColorScheme Overlay(TerminalColorScheme top)
    {
        ArgumentNullException.ThrowIfNull(top);

        var palette = new Dictionary<int, TerminalColor>(Entries);
        foreach (var (index, color) in top.Entries)
        {
            palette[index] = color;
        }

        return new TerminalColorScheme(
            top.Foreground ?? Foreground,
            top.Background ?? Background,
            top.CursorColor ?? CursorColor,
            palette);
    }

    /// <summary>Throws if a palette index is outside 0 to 255.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A palette index is out of range.</exception>
    public void Validate()
    {
        foreach (var index in Entries.Keys)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, PaletteSize);
        }
    }
}
