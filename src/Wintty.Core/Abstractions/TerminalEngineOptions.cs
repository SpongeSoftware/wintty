namespace Wintty.Core.Abstractions;

/// <summary>Initial configuration for a terminal engine instance.</summary>
/// <param name="Columns">Width of the grid in cells; must be positive.</param>
/// <param name="Rows">Height of the grid in cells; must be positive.</param>
/// <param name="MaxScrollback">Maximum number of scrollback lines retained.</param>
/// <param name="Foreground">Default text colour, reported to programs that query it; defaults to white.</param>
/// <param name="Background">Default background colour, reported to programs that query it; defaults to black.</param>
public sealed record TerminalEngineOptions(
    int Columns,
    int Rows,
    int MaxScrollback = 10_000,
    TerminalColor? Foreground = null,
    TerminalColor? Background = null)
{
    /// <summary>The default text colour in effect.</summary>
    public TerminalColor EffectiveForeground => Foreground ?? new TerminalColor(255, 255, 255);

    /// <summary>The default background colour in effect.</summary>
    public TerminalColor EffectiveBackground => Background ?? new TerminalColor(0, 0, 0);

    /// <summary>Throws if the options are not usable.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive or the scrollback is negative.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Rows, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxScrollback);
    }
}
