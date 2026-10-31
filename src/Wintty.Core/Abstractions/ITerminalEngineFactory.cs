namespace Wintty.Core.Abstractions;

/// <summary>Creates <see cref="ITerminalEngine"/> instances.</summary>
public interface ITerminalEngineFactory
{
    /// <summary>Creates a terminal engine.</summary>
    /// <param name="options">Initial dimensions and scrollback.</param>
    /// <returns>A new engine that the caller must dispose.</returns>
    ITerminalEngine Create(TerminalEngineOptions options);
}
