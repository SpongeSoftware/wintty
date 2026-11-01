namespace Wintty.Core.Configuration;

/// <summary>A problem found while reading the config file.</summary>
/// <param name="Line">One-based line number, or 0 when the problem concerns the whole file.</param>
/// <param name="Message">What is wrong, suitable for showing to the user.</param>
public readonly record struct ConfigDiagnostic(int Line, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => Line > 0 ? $"line {Line}: {Message}" : Message;
}

/// <summary>The outcome of reading configuration: the settings that were valid plus any problems.</summary>
/// <param name="Config">The resulting settings; invalid lines leave the default in place.</param>
/// <param name="Diagnostics">Problems found, in file order.</param>
public sealed record ConfigLoadResult(WinttyConfig Config, IReadOnlyList<ConfigDiagnostic> Diagnostics)
{
    /// <summary>
    /// The colours to apply: the theme named in the file with the file's own colour options layered on top.
    /// </summary>
    public Abstractions.TerminalColorScheme Colors { get; init; } = Abstractions.TerminalColorScheme.Default;

    /// <summary>The key bindings in force: the defaults with the file's <c>keybind</c> lines applied.</summary>
    public IReadOnlyDictionary<Keybindings.KeyTrigger, Keybindings.TerminalAction> Bindings { get; init; } =
        Keybindings.Keybinds.Resolve([], false, new List<ConfigDiagnostic>());
}
