using System.Text;

namespace Wintty.Render;

/// <summary>Which font a run of text should be drawn with.</summary>
public enum FontRole
{
    /// <summary>The user's main terminal font.</summary>
    Primary = 0,

    /// <summary>
    /// A symbols font (a Nerd Font): used for private-use glyphs such as file-type icons, Powerline separators and
    /// git icons, which ordinary fonts do not contain and the system cannot fall back to.
    /// </summary>
    Symbols = 1,
}

/// <summary>Decides which font role a piece of text needs.</summary>
public static class GlyphClassifier
{
    /// <summary>Whether a code point is in one of Unicode's private-use areas.</summary>
    /// <param name="codepoint">The Unicode code point.</param>
    public static bool IsPrivateUse(int codepoint) =>
        codepoint is >= 0xE000 and <= 0xF8FF
            or >= 0xF0000 and <= 0xFFFFD
            or >= 0x100000 and <= 0x10FFFD;

    /// <summary>Classifies the text of one cell by its first code point.</summary>
    /// <param name="text">The cell's grapheme cluster; may be empty.</param>
    /// <returns><see cref="FontRole.Symbols"/> for private-use glyphs, otherwise <see cref="FontRole.Primary"/>.</returns>
    public static FontRole RoleOf(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return FontRole.Primary;
        }

        var first = text.EnumerateRunes().First().Value;
        return IsPrivateUse(first) ? FontRole.Symbols : FontRole.Primary;
    }
}
