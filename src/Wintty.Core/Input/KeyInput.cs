namespace Wintty.Core.Input;

/// <summary>What happened to a key.</summary>
public enum KeyAction
{
    /// <summary>The key was released.</summary>
    Release = 0,

    /// <summary>The key went down.</summary>
    Press = 1,

    /// <summary>The key is held and auto-repeating.</summary>
    Repeat = 2,
}

/// <summary>Modifier keys held during a key event.</summary>
[Flags]
public enum KeyModifiers : ushort
{
    /// <summary>No modifiers.</summary>
    None = 0,

    /// <summary>Shift is held.</summary>
    Shift = 1,

    /// <summary>Control is held.</summary>
    Control = 2,

    /// <summary>Alt (Option) is held.</summary>
    Alt = 4,

    /// <summary>The Windows (Super) key is held.</summary>
    Super = 8,

    /// <summary>Caps Lock is on.</summary>
    CapsLock = 16,

    /// <summary>Num Lock is on.</summary>
    NumLock = 32,
}

/// <summary>
/// A keyboard event in terminal terms: the physical key, modifiers and the text the layout produced.
/// The engine turns this into the bytes a program expects, honouring the terminal's current modes.
/// </summary>
/// <param name="Key">The physical key.</param>
/// <param name="Modifiers">Modifiers held.</param>
/// <param name="Action">Press, repeat or release.</param>
/// <param name="Text">
/// Text the key produced under the current layout and modifiers, or <see langword="null"/> when it produced none
/// (or only a control character).
/// </param>
/// <param name="UnshiftedCodepoint">The Unicode code point the key produces with no modifiers, or 0 if unknown.</param>
/// <param name="ConsumedModifiers">Modifiers already reflected in <paramref name="Text"/> (typically Shift).</param>
public readonly record struct KeyInput(
    TerminalKey Key,
    KeyModifiers Modifiers = KeyModifiers.None,
    KeyAction Action = KeyAction.Press,
    string? Text = null,
    int UnshiftedCodepoint = 0,
    KeyModifiers ConsumedModifiers = KeyModifiers.None);
