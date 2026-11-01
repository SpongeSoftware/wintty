namespace Wintty.Core.Configuration;

/// <summary>Reads the wintty config file from disk.</summary>
public static class ConfigLoader
{
    /// <summary>The default config location, <c>%APPDATA%\wintty\config</c>.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "wintty", "config");

    /// <summary>Loads and parses a config file. A missing file is not an error and yields the defaults.</summary>
    /// <param name="path">The file to read.</param>
    /// <param name="themesDirectory">Folder of user theme files, or <see langword="null"/> to use built-in themes only.</param>
    /// <returns>The settings and resolved colours, with a whole-file diagnostic if the file could not be read.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public static ConfigLoadResult Load(string path, string? themesDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            if (!File.Exists(path))
            {
                return new ConfigLoadResult(new WinttyConfig(), []);
            }

            var parsed = ConfigParser.Parse(ReadShared(path));
            var diagnostics = parsed.Diagnostics.ToList();
            var colors = ThemeCatalog.Resolve(parsed.Config, themesDirectory, diagnostics);
            diagnostics.Sort((a, b) => a.Line.CompareTo(b.Line));
            return new ConfigLoadResult(parsed.Config, diagnostics) { Colors = colors };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ConfigLoadResult(new WinttyConfig(), [new ConfigDiagnostic(0, $"could not read {path}: {ex.Message}")]);
        }
    }

    // Editors may still hold the file open while writing, so allow other writers.
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
