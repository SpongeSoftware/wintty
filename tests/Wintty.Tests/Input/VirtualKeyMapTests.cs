using Wintty.Core.Input;
using Wintty.Input;
using Xunit;

namespace Wintty.Tests.Input;

public class VirtualKeyMapTests
{
    [Theory]
    [InlineData(0x41, TerminalKey.A)]
    [InlineData(0x5A, TerminalKey.Z)]
    [InlineData(0x30, TerminalKey.Digit0)]
    [InlineData(0x39, TerminalKey.Digit9)]
    [InlineData(0x70, TerminalKey.F1)]
    [InlineData(0x7B, TerminalKey.F12)]
    [InlineData(0x87, TerminalKey.F24)]
    [InlineData(0x60, TerminalKey.Numpad0)]
    [InlineData(0x69, TerminalKey.Numpad9)]
    public void Map_Ranges(int virtualKey, TerminalKey expected) => Assert.Equal(expected, VirtualKeyMap.Map(virtualKey));

    [Theory]
    [InlineData(0x08, TerminalKey.Backspace)]
    [InlineData(0x09, TerminalKey.Tab)]
    [InlineData(0x0D, TerminalKey.Enter)]
    [InlineData(0x10, TerminalKey.ShiftLeft)]
    [InlineData(0xA0, TerminalKey.ShiftLeft)]
    [InlineData(0xA1, TerminalKey.ShiftRight)]
    [InlineData(0x11, TerminalKey.ControlLeft)]
    [InlineData(0xA2, TerminalKey.ControlLeft)]
    [InlineData(0xA3, TerminalKey.ControlRight)]
    [InlineData(0x12, TerminalKey.AltLeft)]
    [InlineData(0xA4, TerminalKey.AltLeft)]
    [InlineData(0xA5, TerminalKey.AltRight)]
    [InlineData(0x14, TerminalKey.CapsLock)]
    [InlineData(0x1B, TerminalKey.Escape)]
    [InlineData(0x20, TerminalKey.Space)]
    [InlineData(0x21, TerminalKey.PageUp)]
    [InlineData(0x22, TerminalKey.PageDown)]
    [InlineData(0x23, TerminalKey.End)]
    [InlineData(0x24, TerminalKey.Home)]
    [InlineData(0x25, TerminalKey.ArrowLeft)]
    [InlineData(0x26, TerminalKey.ArrowUp)]
    [InlineData(0x27, TerminalKey.ArrowRight)]
    [InlineData(0x28, TerminalKey.ArrowDown)]
    [InlineData(0x2D, TerminalKey.Insert)]
    [InlineData(0x2E, TerminalKey.Delete)]
    [InlineData(0x5B, TerminalKey.MetaLeft)]
    [InlineData(0x5C, TerminalKey.MetaRight)]
    [InlineData(0x5D, TerminalKey.ContextMenu)]
    [InlineData(0x6A, TerminalKey.NumpadMultiply)]
    [InlineData(0x6B, TerminalKey.NumpadAdd)]
    [InlineData(0x6D, TerminalKey.NumpadSubtract)]
    [InlineData(0x6E, TerminalKey.NumpadDecimal)]
    [InlineData(0x6F, TerminalKey.NumpadDivide)]
    [InlineData(0x90, TerminalKey.NumLock)]
    [InlineData(0xBA, TerminalKey.Semicolon)]
    [InlineData(0xBB, TerminalKey.Equal)]
    [InlineData(0xBC, TerminalKey.Comma)]
    [InlineData(0xBD, TerminalKey.Minus)]
    [InlineData(0xBE, TerminalKey.Period)]
    [InlineData(0xBF, TerminalKey.Slash)]
    [InlineData(0xC0, TerminalKey.Backquote)]
    [InlineData(0xDB, TerminalKey.BracketLeft)]
    [InlineData(0xDC, TerminalKey.Backslash)]
    [InlineData(0xDD, TerminalKey.BracketRight)]
    [InlineData(0xDE, TerminalKey.Quote)]
    public void Map_SingleKeys(int virtualKey, TerminalKey expected) => Assert.Equal(expected, VirtualKeyMap.Map(virtualKey));

    [Fact]
    public void Map_ExtendedEnterIsNumpadEnter()
    {
        Assert.Equal(TerminalKey.NumpadEnter, VirtualKeyMap.Map(0x0D, isExtended: true));
        Assert.Equal(TerminalKey.Enter, VirtualKeyMap.Map(0x0D, isExtended: false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0xE5)]
    [InlineData(0xFF)]
    public void Map_UnknownKeysAreUnidentified(int virtualKey) =>
        Assert.Equal(TerminalKey.Unidentified, VirtualKeyMap.Map(virtualKey));

    [Fact]
    public void ProcessKey_IsTheImeCode() => Assert.Equal(0xE5, VirtualKeyMap.ProcessKey);

    [Theory]
    [InlineData(TerminalKey.ShiftLeft, true)]
    [InlineData(TerminalKey.ShiftRight, true)]
    [InlineData(TerminalKey.ControlLeft, true)]
    [InlineData(TerminalKey.ControlRight, true)]
    [InlineData(TerminalKey.AltLeft, true)]
    [InlineData(TerminalKey.AltRight, true)]
    [InlineData(TerminalKey.MetaLeft, true)]
    [InlineData(TerminalKey.MetaRight, true)]
    [InlineData(TerminalKey.CapsLock, true)]
    [InlineData(TerminalKey.NumLock, true)]
    [InlineData(TerminalKey.A, false)]
    [InlineData(TerminalKey.Enter, false)]
    [InlineData(TerminalKey.Unidentified, false)]
    public void IsModifier_OnlyForModifierKeys(TerminalKey key, bool expected) => Assert.Equal(expected, VirtualKeyMap.IsModifier(key));
}
