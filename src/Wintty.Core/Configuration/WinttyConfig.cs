namespace Wintty.Core.Configuration;

/// <summary>User settings read from the wintty config file. Every property has a usable default.</summary>
public sealed record WinttyConfig
{
    /// <summary>Default font family.</summary>
    public const string DefaultFontFamily = "Cascadia Mono";

    /// <summary>Default font size in device-independent pixels.</summary>
    public const float DefaultFontSize = 15f;

    /// <summary>Default padding around the cell grid in device-independent pixels.</summary>
    public const double DefaultWindowPadding = 6;

    /// <summary>Default number of scrollback lines kept.</summary>
    public const int DefaultScrollbackLimit = 10_000;

    /// <summary>Font family for terminal text. Applied live.</summary>
    public string FontFamily { get; init; } = DefaultFontFamily;

    /// <summary>Font size in device-independent pixels. Applied live.</summary>
    public float FontSize { get; init; } = DefaultFontSize;

    /// <summary>Whether the cursor blinks. Applied live.</summary>
    public bool CursorBlink { get; init; } = true;

    /// <summary>Maximum scrollback lines. Applies to new terminals.</summary>
    public int ScrollbackLimit { get; init; } = DefaultScrollbackLimit;

    /// <summary>Padding in device-independent pixels around the cell grid on every edge. Applied live.</summary>
    public double WindowPadding { get; init; } = DefaultWindowPadding;

    /// <summary>The shell to launch, or <see langword="null"/> to auto-detect. Applies to new terminals.</summary>
    public string? Command { get; init; }
}
