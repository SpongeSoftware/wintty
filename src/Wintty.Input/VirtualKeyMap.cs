using Wintty.Core.Input;

namespace Wintty.Input;

/// <summary>Maps Windows virtual-key codes (<c>VK_*</c>) to <see cref="TerminalKey"/>.</summary>
public static class VirtualKeyMap
{
    /// <summary>The virtual-key code Windows sends while an IME is processing input (<c>VK_PROCESSKEY</c>).</summary>
    public const int ProcessKey = 0xE5;

    /// <summary>Maps a virtual-key code to a terminal key.</summary>
    /// <param name="virtualKey">The <c>VK_*</c> code.</param>
    /// <param name="isExtended">Whether the key event carried the extended-key flag (distinguishes numpad Enter).</param>
    /// <returns>The key, or <see cref="TerminalKey.Unidentified"/> when there is no mapping.</returns>
    public static TerminalKey Map(int virtualKey, bool isExtended = false)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return TerminalKey.A + (virtualKey - 0x41);
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            return TerminalKey.Digit0 + (virtualKey - 0x30);
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return TerminalKey.F1 + (virtualKey - 0x70);
        }

        if (virtualKey is >= 0x60 and <= 0x69)
        {
            return TerminalKey.Numpad0 + (virtualKey - 0x60);
        }

        return virtualKey switch
        {
            0x08 => TerminalKey.Backspace,
            0x09 => TerminalKey.Tab,
            0x0D => isExtended ? TerminalKey.NumpadEnter : TerminalKey.Enter,
            0x10 or 0xA0 => TerminalKey.ShiftLeft,
            0xA1 => TerminalKey.ShiftRight,
            0x11 or 0xA2 => TerminalKey.ControlLeft,
            0xA3 => TerminalKey.ControlRight,
            0x12 or 0xA4 => TerminalKey.AltLeft,
            0xA5 => TerminalKey.AltRight,
            0x14 => TerminalKey.CapsLock,
            0x1B => TerminalKey.Escape,
            0x20 => TerminalKey.Space,
            0x21 => TerminalKey.PageUp,
            0x22 => TerminalKey.PageDown,
            0x23 => TerminalKey.End,
            0x24 => TerminalKey.Home,
            0x25 => TerminalKey.ArrowLeft,
            0x26 => TerminalKey.ArrowUp,
            0x27 => TerminalKey.ArrowRight,
            0x28 => TerminalKey.ArrowDown,
            0x2D => TerminalKey.Insert,
            0x2E => TerminalKey.Delete,
            0x5B => TerminalKey.MetaLeft,
            0x5C => TerminalKey.MetaRight,
            0x5D => TerminalKey.ContextMenu,
            0x6A => TerminalKey.NumpadMultiply,
            0x6B => TerminalKey.NumpadAdd,
            0x6D => TerminalKey.NumpadSubtract,
            0x6E => TerminalKey.NumpadDecimal,
            0x6F => TerminalKey.NumpadDivide,
            0x90 => TerminalKey.NumLock,
            0xBA => TerminalKey.Semicolon,
            0xBB => TerminalKey.Equal,
            0xBC => TerminalKey.Comma,
            0xBD => TerminalKey.Minus,
            0xBE => TerminalKey.Period,
            0xBF => TerminalKey.Slash,
            0xC0 => TerminalKey.Backquote,
            0xDB => TerminalKey.BracketLeft,
            0xDC => TerminalKey.Backslash,
            0xDD => TerminalKey.BracketRight,
            0xDE => TerminalKey.Quote,
            _ => TerminalKey.Unidentified,
        };
    }

    /// <summary>Whether a key only modifies other keys and produces no output of its own.</summary>
    /// <param name="key">The key to test.</param>
    public static bool IsModifier(TerminalKey key) => key is
        TerminalKey.ShiftLeft or TerminalKey.ShiftRight
        or TerminalKey.ControlLeft or TerminalKey.ControlRight
        or TerminalKey.AltLeft or TerminalKey.AltRight
        or TerminalKey.MetaLeft or TerminalKey.MetaRight
        or TerminalKey.CapsLock or TerminalKey.NumLock;
}
