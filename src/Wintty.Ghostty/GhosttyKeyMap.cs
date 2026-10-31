using LibGhostty;
using Wintty.Core.Input;

namespace Wintty.Ghostty;

/// <summary>Translates Core key types into libghostty's native key codes.</summary>
internal static class GhosttyKeyMap
{
    private const int NativeActionRelease = 0;
    private const int NativeActionRepeat = 2;

    /// <summary>Maps a key to libghostty's key code; returns 0 for keys it has no code for.</summary>
    internal static int ToNative(TerminalKey key)
    {
        if (key is >= TerminalKey.A and <= TerminalKey.Z)
        {
            return GhosttyNativeLibrary.KeyA + (key - TerminalKey.A);
        }

        if (key is >= TerminalKey.Digit0 and <= TerminalKey.Digit9)
        {
            return GhosttyNativeLibrary.KeyDigit0 + (key - TerminalKey.Digit0);
        }

        if (key is >= TerminalKey.F1 and <= TerminalKey.F24)
        {
            return GhosttyNativeLibrary.KeyF1 + (key - TerminalKey.F1);
        }

        if (key is >= TerminalKey.Numpad0 and <= TerminalKey.Numpad9)
        {
            return GhosttyNativeLibrary.KeyNumpad0 + (key - TerminalKey.Numpad0);
        }

        return key switch
        {
            TerminalKey.Backquote => GhosttyNativeLibrary.KeyBackquote,
            TerminalKey.Backslash => GhosttyNativeLibrary.KeyBackslash,
            TerminalKey.BracketLeft => GhosttyNativeLibrary.KeyBracketLeft,
            TerminalKey.BracketRight => GhosttyNativeLibrary.KeyBracketRight,
            TerminalKey.Comma => GhosttyNativeLibrary.KeyComma,
            TerminalKey.Equal => GhosttyNativeLibrary.KeyEqual,
            TerminalKey.Minus => GhosttyNativeLibrary.KeyMinus,
            TerminalKey.Period => GhosttyNativeLibrary.KeyPeriod,
            TerminalKey.Quote => GhosttyNativeLibrary.KeyQuote,
            TerminalKey.Semicolon => GhosttyNativeLibrary.KeySemicolon,
            TerminalKey.Slash => GhosttyNativeLibrary.KeySlash,
            TerminalKey.Backspace => GhosttyNativeLibrary.KeyBackspace,
            TerminalKey.Enter => GhosttyNativeLibrary.KeyEnter,
            TerminalKey.Space => GhosttyNativeLibrary.KeySpace,
            TerminalKey.Tab => GhosttyNativeLibrary.KeyTab,
            TerminalKey.Escape => GhosttyNativeLibrary.KeyEscape,
            TerminalKey.Delete => GhosttyNativeLibrary.KeyDelete,
            TerminalKey.End => GhosttyNativeLibrary.KeyEnd,
            TerminalKey.Home => GhosttyNativeLibrary.KeyHome,
            TerminalKey.Insert => GhosttyNativeLibrary.KeyInsert,
            TerminalKey.PageDown => GhosttyNativeLibrary.KeyPageDown,
            TerminalKey.PageUp => GhosttyNativeLibrary.KeyPageUp,
            TerminalKey.ArrowDown => GhosttyNativeLibrary.KeyArrowDown,
            TerminalKey.ArrowLeft => GhosttyNativeLibrary.KeyArrowLeft,
            TerminalKey.ArrowRight => GhosttyNativeLibrary.KeyArrowRight,
            TerminalKey.ArrowUp => GhosttyNativeLibrary.KeyArrowUp,
            TerminalKey.NumpadAdd => GhosttyNativeLibrary.KeyNumpadAdd,
            TerminalKey.NumpadSubtract => GhosttyNativeLibrary.KeyNumpadSubtract,
            TerminalKey.NumpadMultiply => GhosttyNativeLibrary.KeyNumpadMultiply,
            TerminalKey.NumpadDivide => GhosttyNativeLibrary.KeyNumpadDivide,
            TerminalKey.NumpadDecimal => GhosttyNativeLibrary.KeyNumpadDecimal,
            TerminalKey.NumpadEnter => GhosttyNativeLibrary.KeyNumpadEnter,
            TerminalKey.ShiftLeft => GhosttyNativeLibrary.KeyShiftLeft,
            TerminalKey.ShiftRight => GhosttyNativeLibrary.KeyShiftRight,
            TerminalKey.ControlLeft => GhosttyNativeLibrary.KeyControlLeft,
            TerminalKey.ControlRight => GhosttyNativeLibrary.KeyControlRight,
            TerminalKey.AltLeft => GhosttyNativeLibrary.KeyAltLeft,
            TerminalKey.AltRight => GhosttyNativeLibrary.KeyAltRight,
            TerminalKey.MetaLeft => GhosttyNativeLibrary.KeyMetaLeft,
            TerminalKey.MetaRight => GhosttyNativeLibrary.KeyMetaRight,
            TerminalKey.CapsLock => GhosttyNativeLibrary.KeyCapsLock,
            TerminalKey.NumLock => GhosttyNativeLibrary.KeyNumLock,
            TerminalKey.ContextMenu => GhosttyNativeLibrary.KeyContextMenu,
            _ => 0,
        };
    }

    /// <summary>Maps a key action to libghostty's action code.</summary>
    internal static int ToNative(KeyAction action) => action switch
    {
        KeyAction.Release => NativeActionRelease,
        KeyAction.Repeat => NativeActionRepeat,
        _ => GhosttyNativeLibrary.KeyActionPress,
    };

    /// <summary>Maps modifiers to libghostty's modifier mask (the bit layout is shared).</summary>
    internal static ushort ToNative(KeyModifiers modifiers) => (ushort)modifiers;
}
