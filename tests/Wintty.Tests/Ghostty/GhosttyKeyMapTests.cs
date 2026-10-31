using LibGhostty;
using Wintty.Core.Input;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

public class GhosttyKeyMapTests
{
    [Fact]
    public void Letters_Digits_FunctionKeysAndNumpadMapByOffset()
    {
        Assert.Equal(GhosttyNativeLibrary.KeyA, GhosttyKeyMap.ToNative(TerminalKey.A));
        Assert.Equal(GhosttyNativeLibrary.KeyZ, GhosttyKeyMap.ToNative(TerminalKey.Z));
        Assert.Equal(GhosttyNativeLibrary.KeyDigit0, GhosttyKeyMap.ToNative(TerminalKey.Digit0));
        Assert.Equal(GhosttyNativeLibrary.KeyDigit9, GhosttyKeyMap.ToNative(TerminalKey.Digit9));
        Assert.Equal(GhosttyNativeLibrary.KeyF1, GhosttyKeyMap.ToNative(TerminalKey.F1));
        Assert.Equal(GhosttyNativeLibrary.KeyF24, GhosttyKeyMap.ToNative(TerminalKey.F24));
        Assert.Equal(GhosttyNativeLibrary.KeyNumpad0, GhosttyKeyMap.ToNative(TerminalKey.Numpad0));
    }

    [Fact]
    public void EveryDefinedKeyExceptUnidentifiedHasANativeCode()
    {
        var missing = Enum.GetValues<TerminalKey>()
            .Where(k => k != TerminalKey.Unidentified && GhosttyKeyMap.ToNative(k) == 0)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void NativeCodesAreUnique()
    {
        var codes = Enum.GetValues<TerminalKey>()
            .Where(k => k != TerminalKey.Unidentified)
            .Select(GhosttyKeyMap.ToNative)
            .ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Unidentified_MapsToZero() => Assert.Equal(0, GhosttyKeyMap.ToNative(TerminalKey.Unidentified));

    [Fact]
    public void ActionsMap()
    {
        Assert.Equal(0, GhosttyKeyMap.ToNative(KeyAction.Release));
        Assert.Equal(GhosttyNativeLibrary.KeyActionPress, GhosttyKeyMap.ToNative(KeyAction.Press));
        Assert.Equal(2, GhosttyKeyMap.ToNative(KeyAction.Repeat));
    }

    [Fact]
    public void ModifiersKeepTheirBits() =>
        Assert.Equal((ushort)(KeyModifiers.Shift | KeyModifiers.Alt), GhosttyKeyMap.ToNative(KeyModifiers.Shift | KeyModifiers.Alt));
}
