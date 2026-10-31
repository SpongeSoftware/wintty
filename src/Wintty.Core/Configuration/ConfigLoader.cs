namespace Wintty.Core.Configuration;

/// <summary>Reads the wintty config file from disk.</summary>
public static class ConfigLoader
{
    /// <summary>The default config location, <c>%APPDATA%\wintty\config</c>.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "wintty", "config");

    /// <summary>Loads and parses a config file. A missing file is not an error and yields the defaults.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The settings, with a whole-file diagnostic if the file could not be read.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    public static ConfigLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return File.Exists(path)
                ? ConfigParser.Parse(ReadShared(path))
                : new ConfigLoadResult(new WinttyConfig(), []);
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
