namespace Wintty.Core.Input;

/// <summary>What happened to a mouse button or the pointer.</summary>
public enum MouseAction
{
    /// <summary>A button went down (wheel steps are also reported as presses).</summary>
    Press = 0,

    /// <summary>A button was released.</summary>
    Release = 1,

    /// <summary>The pointer moved, with or without a button held.</summary>
    Motion = 2,
}

/// <summary>A mouse button. Wheel steps are buttons, as in the terminal mouse protocols.</summary>
public enum MouseButton
{
    /// <summary>No button; used for motion with nothing held.</summary>
    None = 0,

    /// <summary>The primary button.</summary>
    Left = 1,

    /// <summary>The secondary button.</summary>
    Right = 2,

    /// <summary>The middle button.</summary>
    Middle = 3,

    /// <summary>One wheel step away from the user.</summary>
    WheelUp = 4,

    /// <summary>One wheel step towards the user.</summary>
    WheelDown = 5,

    /// <summary>One wheel step to the left.</summary>
    WheelLeft = 6,

    /// <summary>One wheel step to the right.</summary>
    WheelRight = 7,
}

/// <summary>
/// A mouse event in terminal terms. The engine turns it into the bytes a program expects, if the program
/// asked for mouse reports, honouring the report format it chose.
/// </summary>
/// <param name="Action">Press, release or motion.</param>
/// <param name="Button">The button involved, or <see cref="MouseButton.None"/> for plain motion.</param>
/// <param name="X">Horizontal position in pixels from the left edge of the cell grid (padding already removed).</param>
/// <param name="Y">Vertical position in pixels from the top edge of the cell grid (padding already removed).</param>
/// <param name="Modifiers">Modifier keys held.</param>
public readonly record struct MouseInput(
    MouseAction Action,
    MouseButton Button,
    double X,
    double Y,
    KeyModifiers Modifiers = KeyModifiers.None);
