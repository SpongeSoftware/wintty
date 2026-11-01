using System.Globalization;
using System.Text;

namespace Wintty.Core.Events;

/// <summary>Something a program asked the terminal to do on the desktop.</summary>
public abstract record TerminalEvent
{
    private protected TerminalEvent()
    {
    }

    /// <summary>
    /// Turns an OSC sequence into the event it asks for.
    /// </summary>
    /// <param name="sequence">The sequence.</param>
    /// <returns>The event, or <see langword="null"/> for sequences wintty does not act on or that are malformed.</returns>
    public static TerminalEvent? From(OscSequence sequence) => sequence.Command switch
    {
        7 => ParseWorkingDirectory(sequence.Payload),
        9 => ParseConEmu(sequence.Payload),
        52 => ParseClipboard(sequence.Payload),
        99 => ParseKittyNotification(sequence.Payload),
        777 => ParseRxvtNotification(sequence.Payload),
        _ => null,
    };

    private static TerminalEvent? ParseWorkingDirectory(string payload)
    {
        // The report is a file URL: file://host/path with percent-encoding.
        if (!payload.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = payload["file://".Length..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0)
        {
            return null;
        }

        var host = rest[..slash];
        var path = Uri.UnescapeDataString(rest[slash..]);
        return new WorkingDirectoryReported(host, path);
    }

    // OSC 9 is a plain notification, except "9;4;state;progress" which is ConEmu's progress report.
    private static TerminalEvent? ParseConEmu(string payload)
    {
        if (payload.StartsWith("4;", StringComparison.Ordinal))
        {
            var parts = payload.Split(';');
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var state) || state > 4)
            {
                return null;
            }

            var percent = 0;
            if (parts.Length > 2 && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out percent))
            {
                return null;
            }

            return new ProgressReported((ProgressState)state, Math.Clamp(percent, 0, 100));
        }

        return payload.Length == 0 ? null : new NotificationRequested(null, payload);
    }

    // OSC 777;notify;title;body
    private static TerminalEvent? ParseRxvtNotification(string payload)
    {
        var parts = payload.Split(';', 3);
        if (parts.Length < 2 || parts[0] != "notify")
        {
            return null;
        }

        return new NotificationRequested(parts[1], parts.Length > 2 ? parts[2] : string.Empty);
    }

    // OSC 99 carries "metadata;payload". Only plain text bodies and titles are understood.
    private static TerminalEvent? ParseKittyNotification(string payload)
    {
        var separator = payload.IndexOf(';', StringComparison.Ordinal);
        if (separator < 0)
        {
            return null;
        }

        var metadata = payload[..separator];
        var text = payload[(separator + 1)..];
        if (text.Length == 0)
        {
            return null;
        }

        var isTitle = metadata.Split(':').Contains("p=title", StringComparer.Ordinal);
        return isTitle ? new NotificationRequested(text, string.Empty) : new NotificationRequested(null, text);
    }

    // OSC 52;selection;base64 writes the clipboard; a "?" asks to read it.
    private static TerminalEvent? ParseClipboard(string payload)
    {
        var parts = payload.Split(';', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var selection = parts[0].Length == 0 ? "c" : parts[0];
        if (parts[1] == "?")
        {
            return new ClipboardRequested(selection, null);
        }

        try
        {
            return new ClipboardRequested(selection, Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>A shell reported its current directory (OSC 7).</summary>
/// <param name="Host">The host name in the report, empty for the local machine.</param>
/// <param name="Path">The path in the shell's own form, for example <c>/C:/Users/me</c> or <c>/home/me</c>.</param>
public sealed record WorkingDirectoryReported(string Host, string Path) : TerminalEvent
{
    /// <summary>
    /// The directory as a Windows path when the report names one (for example <c>/C:/Users/me</c>), or
    /// <see langword="null"/> for paths that exist only inside a Linux distribution.
    /// </summary>
    public string? WindowsPath
    {
        get
        {
            var trimmed = Path.TrimStart('/');
            if (trimmed.Length >= 2 && char.IsAsciiLetter(trimmed[0]) && trimmed[1] == ':')
            {
                return trimmed.Replace('/', '\\');
            }

            return null;
        }
    }
}

/// <summary>A program asked for a desktop notification (OSC 9, 99 or 777).</summary>
/// <param name="Title">The title, or <see langword="null"/> when the program sent only a message.</param>
/// <param name="Body">The message.</param>
public sealed record NotificationRequested(string? Title, string Body) : TerminalEvent;

/// <summary>The progress state a program reports for the taskbar (ConEmu OSC 9;4).</summary>
public enum ProgressState
{
    /// <summary>No progress is shown.</summary>
    Hidden = 0,

    /// <summary>Normal progress with a percentage.</summary>
    Normal = 1,

    /// <summary>The operation failed.</summary>
    Error = 2,

    /// <summary>Progress with no known percentage.</summary>
    Indeterminate = 3,

    /// <summary>The operation is paused or needs attention.</summary>
    Warning = 4,
}

/// <summary>A program reported progress for the taskbar.</summary>
/// <param name="State">What kind of progress to show.</param>
/// <param name="Percent">Percent complete, 0 to 100.</param>
public sealed record ProgressReported(ProgressState State, int Percent) : TerminalEvent;

/// <summary>A program asked to write to or read from the clipboard (OSC 52).</summary>
/// <param name="Selection">Which clipboard, for example <c>c</c>.</param>
/// <param name="Text">The text to write, or <see langword="null"/> for a request to read the clipboard.</param>
public sealed record ClipboardRequested(string Selection, string? Text) : TerminalEvent
{
    /// <summary>Gets a value indicating whether the program wants to read the clipboard rather than write it.</summary>
    public bool IsRead => Text is null;
}
