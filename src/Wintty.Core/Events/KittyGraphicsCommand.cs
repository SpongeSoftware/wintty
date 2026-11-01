using System.Globalization;

namespace Wintty.Core.Events;

/// <summary>What a Kitty graphics command asks for (the <c>a</c> key).</summary>
public enum GraphicsAction
{
    /// <summary>Transmit image data without showing it (<c>t</c>).</summary>
    Transmit,

    /// <summary>Transmit image data and show it at the cursor (<c>T</c>).</summary>
    TransmitAndDisplay,

    /// <summary>Check whether the terminal can display an image, without storing it (<c>q</c>).</summary>
    Query,

    /// <summary>Show an image already transmitted (<c>p</c>).</summary>
    Put,

    /// <summary>Delete images or placements (<c>d</c>).</summary>
    Delete,

    /// <summary>Transmit an animation frame (<c>f</c>).</summary>
    TransmitFrame,

    /// <summary>Control animation (<c>a</c>).</summary>
    Animate,

    /// <summary>Compose animation frames (<c>c</c>).</summary>
    Compose,
}

/// <summary>A command from the Kitty graphics protocol, parsed from an APC <c>G</c> sequence.</summary>
/// <param name="Action">What the command does; the protocol default, when no action is given, is <see cref="GraphicsAction.Transmit"/>.</param>
/// <param name="Keys">Every control key and its value as sent, so features that are not yet understood remain visible.</param>
/// <param name="Data">The payload after the <c>;</c>: base64 image data, or a path for file transmission.</param>
public sealed record KittyGraphicsCommand(GraphicsAction Action, IReadOnlyDictionary<char, string> Keys, string Data)
{
    /// <summary>The pixel format of the data: 24 (RGB), 32 (RGBA, the default) or 100 (PNG).</summary>
    public int Format => Number('f', 32);

    /// <summary>How the data arrives: <c>d</c> direct (default), <c>f</c> file, <c>t</c> temporary file or <c>s</c> shared memory.</summary>
    public char Transmission => Keys.TryGetValue('t', out var value) && value.Length == 1 ? value[0] : 'd';

    /// <summary>Whether the data is zlib-compressed (<c>o=z</c>).</summary>
    public bool Compressed => Keys.TryGetValue('o', out var value) && value == "z";

    /// <summary>The image id (<c>i</c>), or 0 when none was given.</summary>
    public int ImageId => Number('i', 0);

    /// <summary>The image number (<c>I</c>), or 0 when none was given.</summary>
    public int ImageNumber => Number('I', 0);

    /// <summary>The placement id (<c>p</c>), or 0 when none was given.</summary>
    public int PlacementId => Number('p', 0);

    /// <summary>The source width in pixels (<c>s</c>) for raw data.</summary>
    public int Width => Number('s', 0);

    /// <summary>The source height in pixels (<c>v</c>) for raw data.</summary>
    public int Height => Number('v', 0);

    /// <summary>Gets a value indicating whether more chunks of this image follow (<c>m=1</c>).</summary>
    public bool MoreChunks => Number('m', 0) == 1;

    /// <summary>How much the program wants to hear back: 0 everything, 1 errors only, 2 nothing (<c>q</c>).</summary>
    public int Quiet => Number('q', 0);

    /// <summary>The z-index of the placement (<c>z</c>); negative draws below the text.</summary>
    public int ZIndex => Number('z', 0);

    /// <summary>Gets a value indicating whether the cursor stays where it was after placing (<c>C=1</c>).</summary>
    public bool KeepCursor => Number('C', 0) == 1;

    /// <summary>Reads an integer control key.</summary>
    /// <param name="key">The key letter.</param>
    /// <param name="fallback">Value when the key is absent or not a number.</param>
    /// <returns>The value.</returns>
    public int Number(char key, int fallback) =>
        Keys.TryGetValue(key, out var text) && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    /// <summary>Reads the control keys and payload of an APC <c>G</c> sequence.</summary>
    /// <param name="apc">The APC content, starting with <c>G</c> (the <c>ESC _</c> and the terminator are not part of it).</param>
    /// <returns>The command, or <see langword="null"/> when this is not a graphics command or is malformed.</returns>
    public static KittyGraphicsCommand? Parse(string apc)
    {
        ArgumentNullException.ThrowIfNull(apc);
        if (apc.Length == 0 || apc[0] != 'G')
        {
            return null;
        }

        var body = apc[1..];
        var separator = body.IndexOf(';', StringComparison.Ordinal);
        var control = separator < 0 ? body : body[..separator];
        var data = separator < 0 ? string.Empty : body[(separator + 1)..];

        var keys = new Dictionary<char, string>();
        if (control.Length > 0)
        {
            foreach (var pair in control.Split(','))
            {
                if (pair.Length < 2 || pair[1] != '=')
                {
                    return null;
                }

                keys[pair[0]] = pair[2..];
            }
        }

        var action = keys.TryGetValue('a', out var actionText) && actionText.Length == 1 ? ActionFor(actionText[0]) : GraphicsAction.Transmit;
        return action is null ? null : new KittyGraphicsCommand(action.Value, keys, data);
    }

    private static GraphicsAction? ActionFor(char key) => key switch
    {
        't' => GraphicsAction.Transmit,
        'T' => GraphicsAction.TransmitAndDisplay,
        'q' => GraphicsAction.Query,
        'p' => GraphicsAction.Put,
        'd' => GraphicsAction.Delete,
        'f' => GraphicsAction.TransmitFrame,
        'a' => GraphicsAction.Animate,
        'c' => GraphicsAction.Compose,
        _ => null,
    };
}

/// <summary>A program sent a Kitty graphics command.</summary>
/// <param name="Command">The parsed command.</param>
public sealed record GraphicsCommandReceived(KittyGraphicsCommand Command) : TerminalEvent;
