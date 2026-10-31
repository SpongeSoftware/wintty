using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;
using Wintty.Core.Input;

namespace Wintty.App;

/// <summary>Windows keyboard queries: current modifiers and the text a key produces in the active layout.</summary>
internal static class NativeKeyboard
{
    private const uint DoNotChangeKeyboardState = 0x4;

    /// <summary>Reads the modifier keys currently held (and the lock-key toggles).</summary>
    internal static KeyModifiers CurrentModifiers()
    {
        var modifiers = KeyModifiers.None;
        if (IsDown(VirtualKey.Shift)) { modifiers |= KeyModifiers.Shift; }
        if (IsDown(VirtualKey.Control)) { modifiers |= KeyModifiers.Control; }
        if (IsDown(VirtualKey.Menu)) { modifiers |= KeyModifiers.Alt; }
        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows)) { modifiers |= KeyModifiers.Super; }
        if (IsToggled(VirtualKey.CapitalLock)) { modifiers |= KeyModifiers.CapsLock; }
        if (IsToggled(VirtualKey.NumberKeyLock)) { modifiers |= KeyModifiers.NumLock; }
        return modifiers;
    }

    /// <summary>
    /// Translates a key to the text it produces in the active keyboard layout.
    /// </summary>
    /// <returns>
    /// The text, an empty string when the key produces none, or <see langword="null"/> when the key is a dead key
    /// (the composed character will arrive through <c>CharacterReceived</c> instead).
    /// </returns>
    internal static string? TranslateText(int virtualKey, uint scanCode, KeyModifiers modifiers)
    {
        var state = new byte[256];
        if (modifiers.HasFlag(KeyModifiers.Shift)) { state[0x10] = 0x80; }
        if (modifiers.HasFlag(KeyModifiers.Control)) { state[0x11] = 0x80; }
        if (modifiers.HasFlag(KeyModifiers.Alt)) { state[0x12] = 0x80; }
        if (modifiers.HasFlag(KeyModifiers.CapsLock)) { state[0x14] = 0x01; }

        var buffer = new StringBuilder(8);
        var result = ToUnicodeEx((uint)virtualKey, scanCode, state, buffer, buffer.Capacity, DoNotChangeKeyboardState, GetKeyboardLayout(0));
        return result switch
        {
            < 0 => null,
            0 => string.Empty,
            _ => buffer.ToString(0, result),
        };
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private static bool IsToggled(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Locked);

    [DllImport("user32.dll")]
    private static extern nint GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyState,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder buffer,
        int bufferSize,
        uint flags,
        nint keyboardLayout);
}
