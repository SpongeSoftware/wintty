using Wintty.Core.Configuration;
using Xunit;

namespace Wintty.Tests.Configuration;

public sealed class ConfigLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wintty-config-" + Guid.NewGuid().ToString("N"));

    public ConfigLoaderTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void DefaultPath_IsUnderAppDataWintty()
    {
        Assert.EndsWith(Path.Combine("wintty", "config"), ConfigLoader.DefaultPath);
    }

    [Fact]
    public void Load_RejectsEmptyPath() => Assert.Throws<ArgumentException>(() => ConfigLoader.Load(" "));

    [Fact]
    public void Load_MissingFileGivesDefaultsWithoutDiagnostics()
    {
        var result = ConfigLoader.Load(Path.Combine(_directory, "absent"));

        Assert.Equal(new WinttyConfig(), result.Config);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Load_ParsesTheFile()
    {
        var path = Path.Combine(_directory, "config");
        File.WriteAllText(path, "font-size = 18\nbogus = 1");

        var result = ConfigLoader.Load(path);

        Assert.Equal(18f, result.Config.FontSize);
        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public void Load_ReadsAFileAnotherWriterHasOpen()
    {
        var path = Path.Combine(_directory, "config");
        using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        writer.Write("font-size = 16"u8);
        writer.Flush();

        Assert.Equal(16f, ConfigLoader.Load(path).Config.FontSize);
    }

    [Fact]
    public void Load_UnreadablePathIsReportedNotThrown()
    {
        // A directory where the file should be: File.Exists is false, so create a locked-open exclusive file instead.
        var path = Path.Combine(_directory, "locked");
        using var exclusive = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        var result = ConfigLoader.Load(path);

        Assert.Equal(new WinttyConfig(), result.Config);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(0, diagnostic.Line);
        Assert.Contains("could not read", diagnostic.Message);
    }
}
