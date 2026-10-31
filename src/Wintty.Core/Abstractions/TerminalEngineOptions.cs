namespace Wintty.Core.Abstractions;

/// <summary>Initial configuration for a terminal engine instance.</summary>
/// <param name="Columns">Width of the grid in cells; must be positive.</param>
/// <param name="Rows">Height of the grid in cells; must be positive.</param>
/// <param name="MaxScrollback">Maximum number of scrollback lines retained.</param>
public sealed record TerminalEngineOptions(int Columns, int Rows, int MaxScrollback = 10_000)
{
    /// <summary>Throws if the options are not usable.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive or the scrollback is negative.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Rows, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(MaxScrollback);
    }
}
