namespace Wintty.Core.Abstractions;

/// <summary>Where the visible rows sit within the whole history.</summary>
/// <param name="Total">Rows in scrollback plus the screen.</param>
/// <param name="Offset">Index of the first visible row; 0 is the oldest row.</param>
/// <param name="Length">Number of visible rows.</param>
public readonly record struct ScrollbarState(long Total, long Offset, long Length)
{
    /// <summary>The state of a terminal with no history above the screen.</summary>
    public static ScrollbarState None { get; } = default;

    /// <summary><see langword="true"/> when there is history above or below the visible rows.</summary>
    public bool HasHistory => Total > Length && Length > 0;

    /// <summary><see langword="true"/> when the view is above the live bottom of the screen.</summary>
    public bool IsScrolledBack => HasHistory && Offset + Length < Total;
}
