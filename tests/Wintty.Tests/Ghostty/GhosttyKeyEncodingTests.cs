using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Input;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Checks the bytes produced by the real libghostty key encoder.</summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyKeyEncodingTests
{
    private static readonly GhosttyGateway Gateway = new();

    private static ITerminalEngine Engine() => Gateway.Create(new TerminalEngineOptions(80, 24));

    private static string Encode(ITerminalEngine engine, KeyInput input) => Encoding.UTF8.GetString(engine.EncodeKey(input)).Replace("\u001b", "<ESC>");

    private static void Feed(ITerminalEngine engine, string text) => engine.Write(Encoding.UTF8.GetBytes(text));

    [Theory]
    [InlineData(TerminalKey.ArrowUp, "<ESC>[A")]
    [InlineData(TerminalKey.ArrowDown, "<ESC>[B")]
    [InlineData(TerminalKey.ArrowRight, "<ESC>[C")]
    [InlineData(TerminalKey.ArrowLeft, "<ESC>[D")]
    [InlineData(TerminalKey.Home, "<ESC>[H")]
    [InlineData(TerminalKey.End, "<ESC>[F")]
    [InlineData(TerminalKey.PageUp, "<ESC>[5~")]
    [InlineData(TerminalKey.PageDown, "<ESC>[6~")]
    [InlineData(TerminalKey.Insert, "<ESC>[2~")]
    [InlineData(TerminalKey.Delete, "<ESC>[3~")]
    [InlineData(TerminalKey.F1, "<ESC>OP")]
    [InlineData(TerminalKey.F5, "<ESC>[15~")]
    [InlineData(TerminalKey.F12, "<ESC>[24~")]
    [InlineData(TerminalKey.Enter, "\r")]
    [InlineData(TerminalKey.Tab, "\t")]
    [InlineData(TerminalKey.Backspace, "\u007f")]
    [InlineData(TerminalKey.Escape, "<ESC>")]
    public void SpecialKeys_ProduceStandardSequences(TerminalKey key, string expected)
    {
        using var engine = Engine();

        Assert.Equal(expected, Encode(engine, new KeyInput(key)));
    }

    [Fact]
    public void PlainText_IsPassedThrough()
    {
        using var engine = Engine();

        Assert.Equal("a", Encode(engine, new KeyInput(TerminalKey.A, Text: "a", UnshiftedCodepoint: 'a')));
        Assert.Equal("A", Encode(engine, new KeyInput(TerminalKey.A, KeyModifiers.Shift, Text: "A", UnshiftedCodepoint: 'a', ConsumedModifiers: KeyModifiers.Shift)));
    }

    [Theory]
    [InlineData(TerminalKey.C, 'c', "\u0003")]
    [InlineData(TerminalKey.L, 'l', "\u000c")]
    [InlineData(TerminalKey.D, 'd', "\u0004")]
    [InlineData(TerminalKey.Z, 'z', "\u001a")]
    public void Control_Letters_ProduceControlCharacters(TerminalKey key, char unshifted, string expected)
    {
        using var engine = Engine();

        Assert.Equal(expected, Encode(engine, new KeyInput(key, KeyModifiers.Control, UnshiftedCodepoint: unshifted)));
    }

    [Fact]
    public void Alt_Letter_IsEscapePrefixed()
    {
        using var engine = Engine();

        Assert.Equal("<ESC>x", Encode(engine, new KeyInput(TerminalKey.X, KeyModifiers.Alt, Text: "x", UnshiftedCodepoint: 'x')));
    }

    [Fact]
    public void ShiftTab_IsBacktab()
    {
        using var engine = Engine();

        Assert.Equal("<ESC>[Z", Encode(engine, new KeyInput(TerminalKey.Tab, KeyModifiers.Shift)));
    }

    [Fact]
    public void ModifiedArrows_CarryModifierParameter()
    {
        using var engine = Engine();

        Assert.Equal("<ESC>[1;5A", Encode(engine, new KeyInput(TerminalKey.ArrowUp, KeyModifiers.Control)));
        Assert.Equal("<ESC>[1;2D", Encode(engine, new KeyInput(TerminalKey.ArrowLeft, KeyModifiers.Shift)));
    }

    [Fact]
    public void ApplicationCursorKeys_ChangeArrowSequences()
    {
        using var engine = Engine();
        Assert.Equal("<ESC>[A", Encode(engine, new KeyInput(TerminalKey.ArrowUp)));

        Feed(engine, "\u001b[?1h");
        Assert.Equal("<ESC>OA", Encode(engine, new KeyInput(TerminalKey.ArrowUp)));

        Feed(engine, "\u001b[?1l");
        Assert.Equal("<ESC>[A", Encode(engine, new KeyInput(TerminalKey.ArrowUp)));
    }

    [Fact]
    public void KittyProtocol_DisambiguatesEscapeWhenRequested()
    {
        using var engine = Engine();
        Assert.Equal("<ESC>", Encode(engine, new KeyInput(TerminalKey.Escape)));

        Feed(engine, "\u001b[>1u");

        Assert.Equal("<ESC>[27u", Encode(engine, new KeyInput(TerminalKey.Escape)));
    }

    [Fact]
    public void ModifierOnlyKeys_ProduceNothing()
    {
        using var engine = Engine();

        Assert.Empty(engine.EncodeKey(new KeyInput(TerminalKey.ShiftLeft, KeyModifiers.Shift)));
        Assert.Empty(engine.EncodeKey(new KeyInput(TerminalKey.ControlLeft, KeyModifiers.Control)));
    }

    [Fact]
    public void Release_ProducesNothingByDefault()
    {
        using var engine = Engine();

        Assert.Empty(engine.EncodeKey(new KeyInput(TerminalKey.A, Action: KeyAction.Release, Text: "a", UnshiftedCodepoint: 'a')));
    }

    [Fact]
    public void Text_WithMultiByteUtf8_IsEncoded()
    {
        using var engine = Engine();

        Assert.Equal("é", Encode(engine, new KeyInput(TerminalKey.E, Text: "é", UnshiftedCodepoint: 'e')));
    }

    [Fact]
    public void AfterDispose_EncodeThrows()
    {
        var engine = Engine();
        engine.Dispose();

        Assert.Throws<ObjectDisposedException>(() => engine.EncodeKey(new KeyInput(TerminalKey.A)));
    }
}
