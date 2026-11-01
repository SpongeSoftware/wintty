using System.Reflection;
using Wintty.Core.Abstractions;
using Wintty.Core.Configuration;
using Xunit;

namespace Wintty.Tests.Configuration;

public sealed class ThemeCatalogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wintty-themes-" + Guid.NewGuid().ToString("N"));

    public ThemeCatalogTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void BuiltInThemes_IncludeGhosttyCatppuccinGruvboxAndBamboo()
    {
        var names = ThemeCatalog.BuiltInNames;

        Assert.True(names.Count > 800, $"expected the Ghostty set but found {names.Count}");
        foreach (var expected in new[]
        {
            "Catppuccin Latte", "Catppuccin Frappe", "Catppuccin Macchiato", "Catppuccin Mocha",
            "Gruvbox Dark", "Gruvbox Light", "Bamboo", "Bamboo Multiplex", "Bamboo Light",
            "Dracula+", "Nord", "Solarized Dark Higher Contrast", "TokyoNight",
        })
        {
            Assert.Contains(expected, names);
        }
    }

    [Fact]
    public void BuiltInThemes_NamesAreDistinctAfterNormalisation()
    {
        var resources = typeof(ThemeCatalog).Assembly.GetManifestResourceNames().Count(n => n.StartsWith("Wintty.Themes.", StringComparison.Ordinal));

        Assert.Equal(resources, ThemeCatalog.BuiltInNames.Count);
        Assert.Equal(resources, ThemeCatalog.BuiltInNames.Select(ThemeCatalog.Normalize).Distinct().Count());
    }

    [Fact]
    public void EveryBuiltInTheme_DefinesAFullAnsiPaletteAndColours()
    {
        foreach (var name in ThemeCatalog.BuiltInNames)
        {
            Assert.True(ThemeCatalog.TryGet(name, null, out var scheme), name);
            Assert.NotNull(scheme.Foreground);
            Assert.NotNull(scheme.Background);
            for (var index = 0; index < 16; index++)
            {
                Assert.True(scheme.Entries.ContainsKey(index), $"{name} lacks palette entry {index}");
            }

            scheme.Validate();
        }
    }

    [Fact]
    public void TryGet_ReadsKnownColours()
    {
        Assert.True(ThemeCatalog.TryGet("Catppuccin Mocha", null, out var mocha));

        Assert.Equal(new TerminalColor(0x1e, 0x1e, 0x2e), mocha.Background);
        Assert.Equal(new TerminalColor(0xcd, 0xd6, 0xf4), mocha.Foreground);
        Assert.Equal(new TerminalColor(0xf5, 0xe0, 0xdc), mocha.CursorColor);
        Assert.Equal(new TerminalColor(0xf3, 0x8b, 0xa8), mocha.Entries[1]);
    }

    [Theory]
    [InlineData("catppuccin-mocha")]
    [InlineData("CATPPUCCIN MOCHA")]
    [InlineData("catppuccin_mocha")]
    [InlineData("  Catppuccin   Mocha ")]
    public void TryGet_MatchingIgnoresCaseAndSeparators(string name)
    {
        Assert.True(ThemeCatalog.TryGet(name, null, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("no such theme")]
    public void TryGet_UnknownOrBlankIsFalse(string name)
    {
        Assert.False(ThemeCatalog.TryGet(name, null, out var scheme));
        Assert.Equal(TerminalColorScheme.Default, scheme);
    }

    [Fact]
    public void TryGet_UserThemeFileBeatsTheBuiltInOfTheSameName()
    {
        File.WriteAllText(Path.Combine(_directory, "Nord"), "background = #010203\npalette = 0=#040506\n");

        Assert.True(ThemeCatalog.TryGet("Nord", _directory, out var scheme));

        Assert.Equal(new TerminalColor(1, 2, 3), scheme.Background);
    }

    [Fact]
    public void TryGet_UserThemeWithANewNameIsFound()
    {
        File.WriteAllText(Path.Combine(_directory, "mine"), "foreground = #0a0b0c\nfont-size = 99\nnonsense\n");

        Assert.True(ThemeCatalog.TryGet("mine", _directory, out var scheme));

        Assert.Equal(new TerminalColor(10, 11, 12), scheme.Foreground);
    }

    [Theory]
    [InlineData("..\\config")]
    [InlineData("../config")]
    public void TryGet_NamesThatAreNotFileNamesNeverReadOutsideTheFolder(string name)
    {
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_directory)!, "config"), "background = #ffffff");

        Assert.False(ThemeCatalog.TryGet(name, _directory, out _));
    }

    [Fact]
    public void TryGet_UnreadableUserThemeFallsBackToBuiltIn()
    {
        var path = Path.Combine(_directory, "Nord");
        using var exclusive = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        Assert.True(ThemeCatalog.TryGet("Nord", _directory, out var scheme));

        Assert.NotEqual(TerminalColorScheme.Default, scheme);
    }

    [Fact]
    public void Resolve_ExplicitColoursOverrideTheTheme()
    {
        var config = ConfigParser.Parse("palette = 1=#111111\nbackground = #222222\ntheme = Catppuccin Mocha").Config;
        var diagnostics = new List<ConfigDiagnostic>();

        var scheme = ThemeCatalog.Resolve(config, null, diagnostics);

        Assert.Empty(diagnostics);
        Assert.Equal(new TerminalColor(0x22, 0x22, 0x22), scheme.Background);
        Assert.Equal(new TerminalColor(0xcd, 0xd6, 0xf4), scheme.Foreground);
        Assert.Equal(new TerminalColor(0x11, 0x11, 0x11), scheme.Entries[1]);
        Assert.Equal(new TerminalColor(0xa6, 0xe3, 0xa1), scheme.Entries[2]);
    }

    [Fact]
    public void Resolve_WithoutAThemeUsesOnlyTheExplicitColours()
    {
        var config = ConfigParser.Parse("foreground = #333333").Config;

        var scheme = ThemeCatalog.Resolve(config, null, new List<ConfigDiagnostic>());

        Assert.Equal(new TerminalColorScheme(Foreground: new TerminalColor(0x33, 0x33, 0x33)).Foreground, scheme.Foreground);
        Assert.Empty(scheme.Entries);
    }

    [Fact]
    public void Resolve_UnknownThemeIsReportedAtItsLine()
    {
        var config = ConfigParser.Parse("font-size = 12\ntheme = nope\nforeground = #444444").Config;
        var diagnostics = new List<ConfigDiagnostic>();

        var scheme = ThemeCatalog.Resolve(config, null, diagnostics);

        Assert.Equal(new ConfigDiagnostic(2, "unknown theme 'nope'"), Assert.Single(diagnostics));
        Assert.Equal(new TerminalColor(0x44, 0x44, 0x44), scheme.Foreground);
    }

    [Fact]
    public void Resolve_ValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => ThemeCatalog.Resolve(null!, null, []));
        Assert.Throws<ArgumentNullException>(() => ThemeCatalog.Resolve(new WinttyConfig(), null, null!));
    }

    [Fact]
    public void DefaultUserDirectory_IsUnderAppDataWintty() =>
        Assert.EndsWith(Path.Combine("wintty", "themes"), ThemeCatalog.DefaultUserDirectory);
}
