namespace Wintty.Core.Input;

/// <summary>A physical keyboard key, independent of layout and platform.</summary>
public enum TerminalKey
{
    /// <summary>The key is not recognised.</summary>
    Unidentified = 0,

    /// <summary>The A key.</summary>
    A, /// <summary>The B key.</summary>
    B, /// <summary>The C key.</summary>
    C, /// <summary>The D key.</summary>
    D, /// <summary>The E key.</summary>
    E, /// <summary>The F key.</summary>
    F, /// <summary>The G key.</summary>
    G, /// <summary>The H key.</summary>
    H, /// <summary>The I key.</summary>
    I, /// <summary>The J key.</summary>
    J, /// <summary>The K key.</summary>
    K, /// <summary>The L key.</summary>
    L, /// <summary>The M key.</summary>
    M, /// <summary>The N key.</summary>
    N, /// <summary>The O key.</summary>
    O, /// <summary>The P key.</summary>
    P, /// <summary>The Q key.</summary>
    Q, /// <summary>The R key.</summary>
    R, /// <summary>The S key.</summary>
    S, /// <summary>The T key.</summary>
    T, /// <summary>The U key.</summary>
    U, /// <summary>The V key.</summary>
    V, /// <summary>The W key.</summary>
    W, /// <summary>The X key.</summary>
    X, /// <summary>The Y key.</summary>
    Y, /// <summary>The Z key.</summary>
    Z,

    /// <summary>The 0 key on the main row.</summary>
    Digit0, /// <summary>The 1 key on the main row.</summary>
    Digit1, /// <summary>The 2 key on the main row.</summary>
    Digit2, /// <summary>The 3 key on the main row.</summary>
    Digit3, /// <summary>The 4 key on the main row.</summary>
    Digit4, /// <summary>The 5 key on the main row.</summary>
    Digit5, /// <summary>The 6 key on the main row.</summary>
    Digit6, /// <summary>The 7 key on the main row.</summary>
    Digit7, /// <summary>The 8 key on the main row.</summary>
    Digit8, /// <summary>The 9 key on the main row.</summary>
    Digit9,

    /// <summary>The backtick / tilde key.</summary>
    Backquote, /// <summary>The backslash / pipe key.</summary>
    Backslash, /// <summary>The left bracket key.</summary>
    BracketLeft, /// <summary>The right bracket key.</summary>
    BracketRight, /// <summary>The comma key.</summary>
    Comma, /// <summary>The equals / plus key.</summary>
    Equal, /// <summary>The minus / underscore key.</summary>
    Minus, /// <summary>The period key.</summary>
    Period, /// <summary>The quote key.</summary>
    Quote, /// <summary>The semicolon key.</summary>
    Semicolon, /// <summary>The slash / question mark key.</summary>
    Slash,

    /// <summary>The Backspace key.</summary>
    Backspace, /// <summary>The Enter / Return key.</summary>
    Enter, /// <summary>The space bar.</summary>
    Space, /// <summary>The Tab key.</summary>
    Tab, /// <summary>The Escape key.</summary>
    Escape,

    /// <summary>The Delete key.</summary>
    Delete, /// <summary>The End key.</summary>
    End, /// <summary>The Home key.</summary>
    Home, /// <summary>The Insert key.</summary>
    Insert, /// <summary>The Page Down key.</summary>
    PageDown, /// <summary>The Page Up key.</summary>
    PageUp,

    /// <summary>The Down arrow.</summary>
    ArrowDown, /// <summary>The Left arrow.</summary>
    ArrowLeft, /// <summary>The Right arrow.</summary>
    ArrowRight, /// <summary>The Up arrow.</summary>
    ArrowUp,

    /// <summary>Function key F1.</summary>
    F1, /// <summary>Function key F2.</summary>
    F2, /// <summary>Function key F3.</summary>
    F3, /// <summary>Function key F4.</summary>
    F4, /// <summary>Function key F5.</summary>
    F5, /// <summary>Function key F6.</summary>
    F6, /// <summary>Function key F7.</summary>
    F7, /// <summary>Function key F8.</summary>
    F8, /// <summary>Function key F9.</summary>
    F9, /// <summary>Function key F10.</summary>
    F10, /// <summary>Function key F11.</summary>
    F11, /// <summary>Function key F12.</summary>
    F12, /// <summary>Function key F13.</summary>
    F13, /// <summary>Function key F14.</summary>
    F14, /// <summary>Function key F15.</summary>
    F15, /// <summary>Function key F16.</summary>
    F16, /// <summary>Function key F17.</summary>
    F17, /// <summary>Function key F18.</summary>
    F18, /// <summary>Function key F19.</summary>
    F19, /// <summary>Function key F20.</summary>
    F20, /// <summary>Function key F21.</summary>
    F21, /// <summary>Function key F22.</summary>
    F22, /// <summary>Function key F23.</summary>
    F23, /// <summary>Function key F24.</summary>
    F24,

    /// <summary>Numpad 0.</summary>
    Numpad0, /// <summary>Numpad 1.</summary>
    Numpad1, /// <summary>Numpad 2.</summary>
    Numpad2, /// <summary>Numpad 3.</summary>
    Numpad3, /// <summary>Numpad 4.</summary>
    Numpad4, /// <summary>Numpad 5.</summary>
    Numpad5, /// <summary>Numpad 6.</summary>
    Numpad6, /// <summary>Numpad 7.</summary>
    Numpad7, /// <summary>Numpad 8.</summary>
    Numpad8, /// <summary>Numpad 9.</summary>
    Numpad9, /// <summary>Numpad add.</summary>
    NumpadAdd, /// <summary>Numpad subtract.</summary>
    NumpadSubtract, /// <summary>Numpad multiply.</summary>
    NumpadMultiply, /// <summary>Numpad divide.</summary>
    NumpadDivide, /// <summary>Numpad decimal point.</summary>
    NumpadDecimal, /// <summary>Numpad Enter.</summary>
    NumpadEnter,

    /// <summary>Left Shift.</summary>
    ShiftLeft, /// <summary>Right Shift.</summary>
    ShiftRight, /// <summary>Left Control.</summary>
    ControlLeft, /// <summary>Right Control.</summary>
    ControlRight, /// <summary>Left Alt.</summary>
    AltLeft, /// <summary>Right Alt.</summary>
    AltRight, /// <summary>Left Windows key.</summary>
    MetaLeft, /// <summary>Right Windows key.</summary>
    MetaRight, /// <summary>Caps Lock.</summary>
    CapsLock, /// <summary>Num Lock.</summary>
    NumLock, /// <summary>The context menu key.</summary>
    ContextMenu,
}
