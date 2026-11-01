namespace Wintty.Render;

/// <summary>Chooses an installed Nerd Font to draw private-use symbol glyphs with.</summary>
public static class SymbolFontResolver
{
    /// <summary>
    /// Preferred symbol fonts in priority order. Monospace-width ("Mono") variants come first because their
    /// glyphs fit exactly one terminal cell.
    /// </summary>
    public static readonly IReadOnlyList<string> Preferred =
    [
        "Symbols Nerd Font Mono",
        "FiraCode NFM",
        "FiraCode Nerd Font Mono",
        "CaskaydiaCove NFM",
        "CaskaydiaCove Nerd Font Mono",
        "CaskaydiaMono NFM",
        "JetBrainsMono NFM",
        "JetBrainsMono Nerd Font Mono",
        "MesloLGS NFM",
        "Hack NFM",
        "Hack Nerd Font Mono",
    ];

    /// <summary>Finds the best symbol font among the installed families.</summary>
    /// <param name="installedFamilies">Names of the installed font families.</param>
    /// <param name="preferred">Preferred names in priority order; defaults to <see cref="Preferred"/>.</param>
    /// <returns>The family name to use, or <see langword="null"/> when no Nerd Font is installed.</returns>
    public static string? Resolve(IEnumerable<string> installedFamilies, IReadOnlyList<string>? preferred = null)
    {
        ArgumentNullException.ThrowIfNull(installedFamilies);
        var installed = installedFamilies.ToList();
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? family in installed)
        {
            lookup.TryAdd(family, family);
        }

        foreach (string name in preferred ?? Preferred)
        {
            if (lookup.TryGetValue(name, out string? found))
            {
                return found;
            }
        }

        // Any other Mono-width Nerd Font ("... NFM" or "... Nerd Font Mono").
        return installed
            .Where(f => f.EndsWith(" NFM", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(" Nerd Font Mono", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
