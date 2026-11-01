using Wintty.Core.Abstractions;

namespace Wintty.Core.Configuration;

/// <summary>
/// Finds colour themes by name. Built-in themes are embedded in the assembly (the same set Ghostty ships,
/// plus Bamboo); a theme file with the same name in the user's themes folder takes priority.
/// </summary>
public static class ThemeCatalog
{
    private const string ResourcePrefix = "Wintty.Themes.";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> BuiltIn = new(IndexBuiltIn);

    /// <summary>The default folder for user themes, <c>%APPDATA%\wintty\themes</c>.</summary>
    public static string DefaultUserDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "wintty", "themes");

    /// <summary>The display names of all built-in themes, sorted.</summary>
    public static IReadOnlyList<string> BuiltInNames { get; } = BuiltIn.Value.Values.Order(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Looks up a theme, trying the user's folder before the built-in set.</summary>
    /// <param name="name">
    /// The theme name. Matching ignores case and treats spaces, hyphens and underscores alike, so
    /// <c>catppuccin-mocha</c> finds <c>Catppuccin Mocha</c>.
    /// </param>
    /// <param name="userDirectory">Folder of user theme files, or <see langword="null"/> to skip it.</param>
    /// <param name="scheme">The theme's colours when it was found.</param>
    /// <returns><see langword="true"/> when a theme with that name exists.</returns>
    public static bool TryGet(string name, string? userDirectory, out TerminalColorScheme scheme)
    {
        scheme = TerminalColorScheme.Default;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string? text = ReadUserTheme(name, userDirectory) ?? ReadBuiltIn(name);
        if (text is null)
        {
            return false;
        }

        scheme = ConfigParser.ParseTheme(text);
        return true;
    }

    /// <summary>
    /// Works out the colours a configuration asks for: its theme (if any) with the file's own colour options
    /// layered on top. An unknown theme is reported and ignored.
    /// </summary>
    /// <param name="config">The parsed configuration.</param>
    /// <param name="userDirectory">Folder of user theme files, or <see langword="null"/> to skip it.</param>
    /// <param name="diagnostics">Receives a diagnostic when the theme cannot be found.</param>
    /// <returns>The colour scheme to apply.</returns>
    public static TerminalColorScheme Resolve(WinttyConfig config, string? userDirectory, ICollection<ConfigDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (config.Theme is null)
        {
            return config.ExplicitColors;
        }

        if (TryGet(config.Theme, userDirectory, out TerminalColorScheme? theme))
        {
            return theme.Overlay(config.ExplicitColors);
        }

        diagnostics.Add(new ConfigDiagnostic(config.ThemeLine, $"unknown theme '{config.Theme}'"));
        return config.ExplicitColors;
    }

    internal static string Normalize(string name) =>
        string.Join(' ', name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static string? ReadUserTheme(string name, string? directory)
    {
        // A theme name is a file name, never a path.
        if (directory is null || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        try
        {
            string path = Path.Combine(directory, name);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadBuiltIn(string name)
    {
        if (!BuiltIn.Value.TryGetValue(Normalize(name), out string? displayName))
        {
            return null;
        }

        using Stream stream = typeof(ThemeCatalog).Assembly.GetManifestResourceStream(ResourcePrefix + displayName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static Dictionary<string, string> IndexBuiltIn()
    {
        var index = new Dictionary<string, string>();
        foreach (string resource in typeof(ThemeCatalog).Assembly.GetManifestResourceNames())
        {
            if (resource.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                string display = resource[ResourcePrefix.Length..];
                index.TryAdd(Normalize(display), display);
            }
        }

        return index;
    }
}
