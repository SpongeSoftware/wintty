using System.Text;
using Wintty.Core.Input;

namespace Wintty.Core.Keybindings;

/// <summary>A key together with the modifiers that must be held to trigger a binding.</summary>
/// <param name="Key">The physical key.</param>
/// <param name="Modifiers">Shift, Control, Alt and Super only; lock keys are ignored.</param>
public readonly record struct KeyTrigger(TerminalKey Key, KeyModifiers Modifiers)
{
    /// <summary>The modifiers that take part in matching a binding; Caps Lock and Num Lock never do.</summary>
    public const KeyModifiers MatchedModifiers = KeyModifiers.Shift | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Super;

    private static readonly (string Name, TerminalKey Key)[] KeyNames = BuildKeyNames();

    /// <summary>Builds the trigger for a key event, ignoring lock keys.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">Modifiers held, possibly including lock keys.</param>
    /// <returns>The trigger to look up.</returns>
    public static KeyTrigger From(TerminalKey key, KeyModifiers modifiers) => new(key, modifiers & MatchedModifiers);

    /// <summary>Reads a trigger such as <c>ctrl+shift+n</c>.</summary>
    /// <param name="text">Modifiers and one key joined with <c>+</c>, in any order and case.</param>
    /// <param name="trigger">The trigger when parsing succeeded.</param>
    /// <param name="error">Why parsing failed.</param>
    /// <returns><see langword="true"/> when the text is a valid trigger.</returns>
    public static bool TryParse(string text, out KeyTrigger trigger, out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        trigger = default;
        error = null;
        KeyModifiers modifiers = KeyModifiers.None;
        TerminalKey? key = null;
        foreach (string rawPart in text.Split('+'))
        {
            string part = rawPart.Trim().ToLowerInvariant();
            KeyModifiers modifier = ModifierFor(part);
            if (modifier != KeyModifiers.None)
            {
                modifiers |= modifier;
            }
            else if (key is not null)
            {
                error = $"more than one key in '{text.Trim()}'";
                return false;
            }
            else if (TryKey(part) is { } found)
            {
                key = found;
            }
            else
            {
                error = part.Length == 0 ? $"empty part in '{text.Trim()}'" : $"unknown key '{part}'";
                return false;
            }
        }

        if (key is null)
        {
            error = $"no key in '{text.Trim()}'";
            return false;
        }

        trigger = new KeyTrigger(key.Value, modifiers);
        return true;
    }

    /// <summary>Writes the trigger the way it is written in the config file.</summary>
    /// <returns>For example <c>ctrl+shift+n</c>.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        if (Modifiers.HasFlag(KeyModifiers.Control))
        {
            builder.Append("ctrl+");
        }

        if (Modifiers.HasFlag(KeyModifiers.Alt))
        {
            builder.Append("alt+");
        }

        if (Modifiers.HasFlag(KeyModifiers.Shift))
        {
            builder.Append("shift+");
        }

        if (Modifiers.HasFlag(KeyModifiers.Super))
        {
            builder.Append("super+");
        }

        TerminalKey key = Key;
        return builder.Append(KeyNames.FirstOrDefault(n => n.Key == key).Name ?? key.ToString()).ToString();
    }

    private static KeyModifiers ModifierFor(string name) => name switch
    {
        "shift" => KeyModifiers.Shift,
        "ctrl" or "control" => KeyModifiers.Control,
        "alt" or "option" => KeyModifiers.Alt,
        "super" or "win" or "cmd" => KeyModifiers.Super,
        _ => KeyModifiers.None,
    };

    private static TerminalKey? TryKey(string name)
    {
        foreach ((string? candidate, TerminalKey key) in KeyNames)
        {
            if (candidate == name)
            {
                return key;
            }
        }

        return name switch
        {
            "grave" or "grave_accent" => TerminalKey.Backquote,
            "return" => TerminalKey.Enter,
            "esc" => TerminalKey.Escape,
            "del" => TerminalKey.Delete,
            "pageup" or "prior" => TerminalKey.PageUp,
            "pagedown" or "next" => TerminalKey.PageDown,
            _ => null,
        };
    }

    // The first name listed for a key is the one written back out.
    private static (string, TerminalKey)[] BuildKeyNames()
    {
        var names = new List<(string, TerminalKey)>();
        for (char letter = 'a'; letter <= 'z'; letter++)
        {
            names.Add((letter.ToString(), Enum.Parse<TerminalKey>(char.ToUpperInvariant(letter).ToString())));
        }

        for (int digit = 0; digit <= 9; digit++)
        {
            names.Add((digit.ToString(System.Globalization.CultureInfo.InvariantCulture), Enum.Parse<TerminalKey>("Digit" + digit)));
        }

        for (int number = 1; number <= 24; number++)
        {
            names.Add(("f" + number, Enum.Parse<TerminalKey>("F" + number)));
        }

        names.AddRange(
        [
            ("backquote", TerminalKey.Backquote), ("backslash", TerminalKey.Backslash),
            ("bracket_left", TerminalKey.BracketLeft), ("bracket_right", TerminalKey.BracketRight),
            ("comma", TerminalKey.Comma), ("equal", TerminalKey.Equal), ("minus", TerminalKey.Minus),
            ("period", TerminalKey.Period), ("quote", TerminalKey.Quote), ("semicolon", TerminalKey.Semicolon),
            ("slash", TerminalKey.Slash), ("backspace", TerminalKey.Backspace), ("enter", TerminalKey.Enter),
            ("space", TerminalKey.Space), ("tab", TerminalKey.Tab), ("escape", TerminalKey.Escape),
            ("delete", TerminalKey.Delete), ("end", TerminalKey.End), ("home", TerminalKey.Home),
            ("insert", TerminalKey.Insert), ("page_down", TerminalKey.PageDown), ("page_up", TerminalKey.PageUp),
            ("down", TerminalKey.ArrowDown), ("left", TerminalKey.ArrowLeft), ("right", TerminalKey.ArrowRight),
            ("up", TerminalKey.ArrowUp),
        ]);
        return [.. names];
    }
}
