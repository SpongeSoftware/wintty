using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Core.Input;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>
/// Checks the Kitty keyboard protocol flags against the real libghostty key encoder. A program turns a flag set
/// on with <c>CSI &gt; flags u</c>; the encoder then answers with the sequences the protocol defines.
/// Flags: 1 disambiguate, 2 report event types, 4 report alternate keys, 8 report all keys as escape codes,
/// 16 report associated text.
/// </summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyKittyKeyboardTests
{
    private static readonly GhosttyGateway Gateway = new();

    private static ITerminalEngine EngineWithFlags(int flags)
    {
        var engine = Gateway.Create(new TerminalEngineOptions(80, 24));
        engine.Write(Encoding.ASCII.GetBytes("\u001b[>" + flags + "u"));
        return engine;
    }

    private static string Encode(ITerminalEngine engine, KeyInput input) =>
        Encoding.UTF8.GetString(engine.EncodeKey(input)).Replace("\u001b", "<ESC>", StringComparison.Ordinal);

    private static KeyInput A(KeyAction action = KeyAction.Press, KeyModifiers modifiers = KeyModifiers.None, string? text = "a") =>
        new(TerminalKey.A, modifiers, action, text, 'a', modifiers & KeyModifiers.Shift);

    [Fact]
    public void Disambiguate_ReportsEscapeAndModifiedTextKeysAsCodes()
    {
        using var engine = EngineWithFlags(1);

        Assert.Equal("<ESC>[27u", Encode(engine, new KeyInput(TerminalKey.Escape)));
        Assert.Equal("<ESC>[97;3u", Encode(engine, A(modifiers: KeyModifiers.Alt, text: null)));
        Assert.Equal("<ESC>[99;5u", Encode(engine, new KeyInput(TerminalKey.C, KeyModifiers.Control, UnshiftedCodepoint: 'c')));
        Assert.Equal("a", Encode(engine, A()));
    }

    [Fact]
    public void Disambiguate_LeavesReleasesOfTextKeysUnreported()
    {
        using var engine = EngineWithFlags(1);

        Assert.Empty(engine.EncodeKey(A(KeyAction.Release)));
    }

    [Fact]
    public void ReportEventTypes_MarksRepeatAndReleaseOnKeysReportedAsCodes()
    {
        using var engine = EngineWithFlags(3);

        Assert.Equal("<ESC>[27u", Encode(engine, new KeyInput(TerminalKey.Escape)));
        Assert.Equal("<ESC>[27;1:2u", Encode(engine, new KeyInput(TerminalKey.Escape, Action: KeyAction.Repeat)));
        Assert.Equal("<ESC>[27;1:3u", Encode(engine, new KeyInput(TerminalKey.Escape, Action: KeyAction.Release)));
    }

    [Fact]
    public void ReportEventTypes_AlsoMarksModifiedKeys()
    {
        using var engine = EngineWithFlags(3);

        Assert.Equal("<ESC>[97;5:3u", Encode(engine, new KeyInput(TerminalKey.A, KeyModifiers.Control, KeyAction.Release, UnshiftedCodepoint: 'a')));
    }

    [Fact]
    public void ReportAllKeys_SendsEvenPlainTextAsCodes()
    {
        using var engine = EngineWithFlags(8);

        Assert.Equal("<ESC>[97u", Encode(engine, A()));
        Assert.Equal("<ESC>[13u", Encode(engine, new KeyInput(TerminalKey.Enter)));
        Assert.Equal("<ESC>[9u", Encode(engine, new KeyInput(TerminalKey.Tab)));
        Assert.Equal("<ESC>[127u", Encode(engine, new KeyInput(TerminalKey.Backspace)));
    }

    [Fact]
    public void ReportAllKeys_WithEventTypes_ReportsReleasesOfTextKeys()
    {
        using var engine = EngineWithFlags(10);

        Assert.Equal("<ESC>[97;1:3u", Encode(engine, A(KeyAction.Release)));
        Assert.Equal("<ESC>[97;1:2u", Encode(engine, A(KeyAction.Repeat)));
    }

    [Fact]
    public void ReportAllKeys_DeliversModifierOnlyKeys()
    {
        using var engine = EngineWithFlags(8);

        Assert.Equal("<ESC>[57441;2u", Encode(engine, new KeyInput(TerminalKey.ShiftLeft, KeyModifiers.Shift)));
        Assert.Equal("<ESC>[57442;5u", Encode(engine, new KeyInput(TerminalKey.ControlLeft, KeyModifiers.Control)));
        Assert.Equal("<ESC>[57443;3u", Encode(engine, new KeyInput(TerminalKey.AltLeft, KeyModifiers.Alt)));
    }

    [Fact]
    public void ReportAllKeysWithEventTypes_DeliversModifierReleases()
    {
        using var engine = EngineWithFlags(10);

        Assert.Equal("<ESC>[57441;1:3u", Encode(engine, new KeyInput(TerminalKey.ShiftLeft, Action: KeyAction.Release)));
    }

    [Fact]
    public void ModifierOnlyKeys_AreSilentWithoutReportAllKeys()
    {
        using var engine = EngineWithFlags(1);

        Assert.Empty(engine.EncodeKey(new KeyInput(TerminalKey.ShiftLeft, KeyModifiers.Shift)));
    }

    [Fact]
    public void AssociatedText_IsAppendedWhenRequested()
    {
        using var engine = EngineWithFlags(24);

        Assert.Equal("<ESC>[97;;97u", Encode(engine, A()));
        Assert.Equal("<ESC>[97;2;65u", Encode(engine, A(modifiers: KeyModifiers.Shift, text: "A")));
    }

    [Fact]
    public void AlternateKeys_ReportTheShiftedCodePoint()
    {
        using var engine = EngineWithFlags(12);

        Assert.Equal("<ESC>[97:65;2u", Encode(engine, A(modifiers: KeyModifiers.Shift, text: "A")));
    }

    [Fact]
    public void EveryFlagTogether_StillEncodesAKey()
    {
        using var engine = EngineWithFlags(31);

        Assert.StartsWith("<ESC>[97", Encode(engine, A()), StringComparison.Ordinal);
        Assert.StartsWith("<ESC>[97", Encode(engine, A(KeyAction.Release)), StringComparison.Ordinal);
    }

    [Fact]
    public void PoppingTheFlags_RestoresLegacyEncoding()
    {
        using var engine = EngineWithFlags(1);
        Assert.Equal("<ESC>[27u", Encode(engine, new KeyInput(TerminalKey.Escape)));

        engine.Write("\u001b[<u"u8);

        Assert.Equal("<ESC>", Encode(engine, new KeyInput(TerminalKey.Escape)));
    }

    [Fact]
    public void TheMainAndAlternateScreensKeepSeparateFlags()
    {
        using var engine = EngineWithFlags(1);

        engine.Write("\u001b[?1049h"u8);
        Assert.Equal("<ESC>", Encode(engine, new KeyInput(TerminalKey.Escape)));

        engine.Write("\u001b[?1049l"u8);
        Assert.Equal("<ESC>[27u", Encode(engine, new KeyInput(TerminalKey.Escape)));
    }

    [Fact]
    public void ArrowKeysKeepTheirFormAndF1MovesToCsi()
    {
        using var engine = EngineWithFlags(1);

        Assert.Equal("<ESC>[A", Encode(engine, new KeyInput(TerminalKey.ArrowUp)));
        Assert.Equal("<ESC>[1;5A", Encode(engine, new KeyInput(TerminalKey.ArrowUp, KeyModifiers.Control)));

        // The protocol replaces the SS3 form of F1 to F4 with CSI.
        Assert.Equal("<ESC>[P", Encode(engine, new KeyInput(TerminalKey.F1)));
    }
}
