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

    /// <summary>A shared empty palette so default configurations compare equal.</summary>
    public static readonly IReadOnlyDictionary<int, Abstractions.TerminalColor> NoPalette =
        System.Collections.Frozen.FrozenDictionary<int, Abstractions.TerminalColor>.Empty;

    /// <summary>Name of a built-in or user theme, or <see langword="null"/> for none. Applied live.</summary>
    public string? Theme { get; init; }

    /// <summary>Default text colour, overriding the theme. Applied live.</summary>
    public Abstractions.TerminalColor? Foreground { get; init; }

    /// <summary>Default background colour, overriding the theme. Applied live.</summary>
    public Abstractions.TerminalColor? Background { get; init; }

    /// <summary>Cursor colour, overriding the theme. Applied live.</summary>
    public Abstractions.TerminalColor? CursorColor { get; init; }

    /// <summary>Palette entries (index 0 to 255) that override the theme. Applied live.</summary>
    public IReadOnlyDictionary<int, Abstractions.TerminalColor> Palette { get; init; } = NoPalette;

    /// <summary>The colours set directly in the file, before any theme is applied.</summary>
    public Abstractions.TerminalColorScheme ExplicitColors => new(Foreground, Background, CursorColor, Palette);

    /// <summary>The line the <see cref="Theme"/> was set on, used to point diagnostics at it.</summary>
    internal int ThemeLine { get; init; }

    /// <summary>Font family for terminal text. Applied live.</summary>
    public string FontFamily { get; init; } = DefaultFontFamily;

    /// <summary>Font size in device-independent pixels. Applied live.</summary>
    public float FontSize { get; init; } = DefaultFontSize;

    /// <summary>Whether the cursor blinks. Applied live.</summary>
    public bool CursorBlink { get; init; } = true;

    /// <summary>Whether pasting text with line breaks asks for confirmation first. Applied live.</summary>
    public bool PasteProtection { get; init; } = true;

    /// <summary>Maximum scrollback lines. Applies to new terminals.</summary>
    public int ScrollbackLimit { get; init; } = DefaultScrollbackLimit;

    /// <summary>Padding in device-independent pixels around the cell grid on every edge. Applied live.</summary>
    public double WindowPadding { get; init; } = DefaultWindowPadding;

    /// <summary>The shell to launch, or <see langword="null"/> to auto-detect. Applies to new terminals.</summary>
    public string? Command { get; init; }
}
