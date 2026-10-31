using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Colour schemes applied to the real libghostty terminal.</summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyColorSchemeTests
{
    private static readonly GhosttyGateway Gateway = new();
    private static readonly TerminalColor Pink = new(255, 105, 180);
    private static readonly TerminalColor Teal = new(0, 128, 128);

    private static ITerminalEngine Create() => Gateway.Create(new TerminalEngineOptions(20, 3));

    private static void Write(ITerminalEngine engine, string text) => engine.Write(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void SetColors_ChangesTheDefaultForegroundAndBackground()
    {
        using var engine = Create();
        var scheme = new TerminalColorScheme(Pink, Teal);

        engine.SetColors(scheme);
        var snapshot = engine.CreateSnapshot();

        Assert.Equal(Pink, snapshot.DefaultForeground);
        Assert.Equal(Teal, snapshot.DefaultBackground);
    }

    [Fact]
    public void SetColors_OverridesAPaletteEntryUsedByText()
    {
        using var engine = Create();
        engine.SetColors(new TerminalColorScheme(Palette: new Dictionary<int, TerminalColor> { [1] = Pink }));

        Write(engine, "\u001b[31mX\u001b[32mY");
        var snapshot = engine.CreateSnapshot();

        Assert.Equal(Pink, snapshot[0, 0].Foreground);
        Assert.NotEqual(Pink, snapshot[1, 0].Foreground);
    }

    [Fact]
    public void SetColors_PaletteIsReplacedNotAccumulated()
    {
        using var engine = Create();
        engine.SetColors(new TerminalColorScheme(Palette: new Dictionary<int, TerminalColor> { [1] = Pink }));
        engine.SetColors(new TerminalColorScheme(Palette: new Dictionary<int, TerminalColor> { [2] = Teal }));

        Write(engine, "\u001b[31mX\u001b[32mY");
        var snapshot = engine.CreateSnapshot();

        Assert.NotEqual(Pink, snapshot[0, 0].Foreground);
        Assert.Equal(Teal, snapshot[1, 0].Foreground);
    }

    [Fact]
    public void SetColors_DefaultSchemeRestoresTheBuiltInValues()
    {
        using var engine = Create();
        var before = engine.CreateSnapshot();
        engine.SetColors(new TerminalColorScheme(Pink, Teal, Pink, new Dictionary<int, TerminalColor> { [1] = Pink }));

        engine.SetColors(TerminalColorScheme.Default);
        Write(engine, "\u001b[31mX");
        var after = engine.CreateSnapshot();

        Assert.Equal(before.DefaultForeground, after.DefaultForeground);
        Assert.Equal(before.DefaultBackground, after.DefaultBackground);
        Assert.NotEqual(Pink, after[0, 0].Foreground);
        Assert.Null(after.Cursor.Color);
    }

    [Fact]
    public void SetColors_CursorColourIsReportedInTheSnapshot()
    {
        using var engine = Create();
        Assert.Null(engine.CreateSnapshot().Cursor.Color);

        engine.SetColors(new TerminalColorScheme(CursorColor: Pink));

        Assert.Equal(Pink, engine.CreateSnapshot().Cursor.Color);
    }

    [Fact]
    public void SetColors_RejectsNullAndBadIndexes()
    {
        using var engine = Create();

        Assert.Throws<ArgumentNullException>(() => engine.SetColors(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => engine.SetColors(new TerminalColorScheme(Palette: new Dictionary<int, TerminalColor> { [256] = Pink })));
    }

    [Fact]
    public void SetColors_AfterDisposeThrows()
    {
        var engine = Create();
        engine.Dispose();

        Assert.Throws<ObjectDisposedException>(() => engine.SetColors(TerminalColorScheme.Default));
    }

    [Fact]
    public void EveryBundledThemeCanBeApplied()
    {
        using var engine = Create();
        foreach (var name in Wintty.Core.Configuration.ThemeCatalog.BuiltInNames)
        {
            Wintty.Core.Configuration.ThemeCatalog.TryGet(name, null, out var scheme);
            engine.SetColors(scheme);
        }

        var last = engine.CreateSnapshot();
        Assert.True(last.Columns > 0);
    }
}
