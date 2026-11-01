using Wintty.Core.Configuration;
using Wintty.Core.Input;
using Wintty.Core.Keybindings;
using Xunit;

namespace Wintty.Tests.Keybindings;

public class KeyTriggerTests
{
    [Theory]
    [InlineData("ctrl+shift+n", TerminalKey.N, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData("Shift+Ctrl+N", TerminalKey.N, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData("alt+f4", TerminalKey.F4, KeyModifiers.Alt)]
    [InlineData("control+option+page_up", TerminalKey.PageUp, KeyModifiers.Control | KeyModifiers.Alt)]
    [InlineData("super+1", TerminalKey.Digit1, KeyModifiers.Super)]
    [InlineData("win+left", TerminalKey.ArrowLeft, KeyModifiers.Super)]
    [InlineData("tab", TerminalKey.Tab, KeyModifiers.None)]
    [InlineData("ctrl+equal", TerminalKey.Equal, KeyModifiers.Control)]
    [InlineData("ctrl+esc", TerminalKey.Escape, KeyModifiers.Control)]
    [InlineData("ctrl+return", TerminalKey.Enter, KeyModifiers.Control)]
    [InlineData("ctrl+del", TerminalKey.Delete, KeyModifiers.Control)]
    [InlineData("ctrl+pageup", TerminalKey.PageUp, KeyModifiers.Control)]
    [InlineData("ctrl+next", TerminalKey.PageDown, KeyModifiers.Control)]
    [InlineData("ctrl+grave", TerminalKey.Backquote, KeyModifiers.Control)]
    public void TryParse_ReadsModifiersAndKeyInAnyOrderAndCase(string text, TerminalKey key, KeyModifiers modifiers)
    {
        Assert.True(KeyTrigger.TryParse(text, out var trigger, out var error), error);
        Assert.Equal(new KeyTrigger(key, modifiers), trigger);
    }

    [Theory]
    [InlineData("", "empty part")]
    [InlineData("ctrl+", "empty part")]
    [InlineData("ctrl+shift", "no key")]
    [InlineData("ctrl+a+b", "more than one key")]
    [InlineData("ctrl+nonsense", "unknown key 'nonsense'")]
    public void TryParse_ExplainsWhyATriggerIsRejected(string text, string expected)
    {
        Assert.False(KeyTrigger.TryParse(text, out _, out var error));
        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_WritesTheCanonicalForm()
    {
        Assert.Equal("ctrl+alt+shift+super+page_up", new KeyTrigger(TerminalKey.PageUp, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Super).ToString());
        Assert.Equal("a", new KeyTrigger(TerminalKey.A, KeyModifiers.None).ToString());
        Assert.Equal("Numpad5", new KeyTrigger(TerminalKey.Numpad5, KeyModifiers.None).ToString());
    }

    [Fact]
    public void RoundTrip_EveryDefaultTriggerReparsesToItself()
    {
        foreach (var (trigger, _) in Keybinds.Defaults)
        {
            Assert.True(KeyTrigger.TryParse(trigger.ToString(), out var again, out _));
            Assert.Equal(trigger, again);
        }
    }

    [Fact]
    public void From_IgnoresLockKeys() =>
        Assert.Equal(
            new KeyTrigger(TerminalKey.C, KeyModifiers.Control),
            KeyTrigger.From(TerminalKey.C, KeyModifiers.Control | KeyModifiers.CapsLock | KeyModifiers.NumLock));

    [Fact]
    public void TryParse_RejectsNull() => Assert.Throws<ArgumentNullException>(() => KeyTrigger.TryParse(null!, out _, out _));
}

public class KeybindsTests
{
    private static KeybindEntry Entry(string trigger, TerminalAction action, int line)
    {
        Assert.True(KeyTrigger.TryParse(trigger, out var parsed, out _));
        return new KeybindEntry(parsed, action, line);
    }

    [Fact]
    public void EveryActionHasAUniqueName()
    {
        Assert.Equal(Keybinds.Actions.Count, Keybinds.Actions.Select(a => a.Name).Distinct().Count());
        Assert.Equal(Enum.GetValues<TerminalAction>().Length, Keybinds.Actions.Count);
        Assert.All(Keybinds.Actions, a => Assert.False(string.IsNullOrWhiteSpace(a.Description)));
    }

    [Fact]
    public void DefaultsNeverBindTheSameKeyTwice() =>
        Assert.Equal(Keybinds.Defaults.Count, Keybinds.Defaults.Select(d => d.Trigger).Distinct().Count());

    [Fact]
    public void DefaultsCoverEveryActionExceptUnbind()
    {
        foreach (var action in Enum.GetValues<TerminalAction>().Where(a => a != TerminalAction.Unbind))
        {
            Assert.Contains(Keybinds.Defaults, d => d.Action == action);
        }
    }

    [Theory]
    [InlineData("new_tab", TerminalAction.NewTab)]
    [InlineData("NEW_SPLIT:RIGHT", TerminalAction.NewSplitRight)]
    [InlineData(" goto_split:up ", TerminalAction.GotoSplitUp)]
    public void TryParseAction_IsCaseInsensitive(string name, TerminalAction expected)
    {
        Assert.True(Keybinds.TryParseAction(name, out var action));
        Assert.Equal(expected, action);
    }

    [Fact]
    public void TryParseAction_RejectsUnknownNames()
    {
        Assert.False(Keybinds.TryParseAction("new_window", out _));
        Assert.Throws<ArgumentNullException>(() => Keybinds.TryParseAction(null!, out _));
    }

    [Fact]
    public void NameOf_IsTheInverseOfTryParseAction()
    {
        foreach (var info in Keybinds.Actions)
        {
            Assert.Equal(info.Name, Keybinds.NameOf(info.Action));
        }
    }

    [Fact]
    public void TryParseEntry_ReadsTriggerAndAction()
    {
        Assert.True(Keybinds.TryParseEntry("ctrl+shift+n=new_tab", 7, out var entry, out var error), error);
        Assert.Equal(new KeybindEntry(new KeyTrigger(TerminalKey.N, KeyModifiers.Control | KeyModifiers.Shift), TerminalAction.NewTab, 7), entry);
    }

    [Theory]
    [InlineData("ctrl+n", "expected keybind")]
    [InlineData("=new_tab", "expected keybind")]
    [InlineData("ctrl+n=", "expected keybind")]
    [InlineData("ctrl+n=new_window", "unknown action 'new_window'")]
    [InlineData("ctrl+wat=new_tab", "unknown key 'wat'")]
    public void TryParseEntry_ExplainsWhatIsWrong(string value, string expected)
    {
        Assert.False(Keybinds.TryParseEntry(value, 1, out var entry, out var error));
        Assert.Null(entry);
        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_StartsFromTheDefaultsAndUserLinesOverride()
    {
        var diagnostics = new List<ConfigDiagnostic>();

        var map = Keybinds.Resolve([Entry("ctrl+shift+c", TerminalAction.NewTab, 1), Entry("f9", TerminalAction.NextTab, 2)], false, diagnostics);

        Assert.Equal(TerminalAction.NewTab, map[new KeyTrigger(TerminalKey.C, KeyModifiers.Control | KeyModifiers.Shift)]);
        Assert.Equal(TerminalAction.NextTab, map[new KeyTrigger(TerminalKey.F9, KeyModifiers.None)]);
        Assert.Equal(TerminalAction.PasteFromClipboard, map[new KeyTrigger(TerminalKey.V, KeyModifiers.Control | KeyModifiers.Shift)]);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Resolve_UnbindRemovesADefault()
    {
        var map = Keybinds.Resolve([Entry("ctrl+shift+c", TerminalAction.Unbind, 1)], false, new List<ConfigDiagnostic>());

        Assert.DoesNotContain(new KeyTrigger(TerminalKey.C, KeyModifiers.Control | KeyModifiers.Shift), map.Keys);
    }

    [Fact]
    public void Resolve_ClearDropsTheDefaults()
    {
        var map = Keybinds.Resolve([Entry("f9", TerminalAction.NewTab, 1)], true, new List<ConfigDiagnostic>());

        Assert.Single(map);
    }

    [Fact]
    public void Resolve_ReportsAConflictBetweenTwoUserLinesAndTheLaterWins()
    {
        var diagnostics = new List<ConfigDiagnostic>();

        var map = Keybinds.Resolve([Entry("f9", TerminalAction.NewTab, 3), Entry("f9", TerminalAction.NextTab, 5)], true, diagnostics);

        Assert.Equal(TerminalAction.NextTab, map[new KeyTrigger(TerminalKey.F9, KeyModifiers.None)]);
        var note = Assert.Single(diagnostics);
        Assert.Equal(5, note.Line);
        Assert.Contains("already bound on line 3", note.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_RepeatingTheSameBindingIsNotAConflict()
    {
        var diagnostics = new List<ConfigDiagnostic>();

        Keybinds.Resolve([Entry("f9", TerminalAction.NewTab, 1), Entry("f9", TerminalAction.NewTab, 2)], true, diagnostics);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Resolve_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => Keybinds.Resolve(null!, false, []));
        Assert.Throws<ArgumentNullException>(() => Keybinds.Resolve([], false, null!));
    }

    [Fact]
    public void TryParseEntry_RejectsNull() => Assert.Throws<ArgumentNullException>(() => Keybinds.TryParseEntry(null!, 1, out _, out _));
}

public class KeybindConfigTests
{
    [Fact]
    public void KeybindLines_AreAppliedOnTopOfTheDefaults()
    {
        var result = ConfigParser.Parse("keybind = f9=new_tab\nkeybind = ctrl+shift+t=unbind");

        Assert.Empty(result.Diagnostics);
        Assert.Equal(TerminalAction.NewTab, result.Bindings[new KeyTrigger(TerminalKey.F9, KeyModifiers.None)]);
        Assert.DoesNotContain(new KeyTrigger(TerminalKey.T, KeyModifiers.Control | KeyModifiers.Shift), result.Bindings.Keys);
    }

    [Fact]
    public void ClearThenBindings_LeavesOnlyTheUsersBindings()
    {
        var result = ConfigParser.Parse("keybind = clear\nkeybind = f9=new_tab");

        Assert.Single(result.Bindings);
        Assert.True(result.Config.ClearDefaultKeybinds);
    }

    [Fact]
    public void AnEmptyValue_ForgetsEarlierKeybindLines()
    {
        var config = ConfigParser.Parse("keybind = clear\nkeybind = f9=new_tab\nkeybind =").Config;

        Assert.Equal(new WinttyConfig(), config);
    }

    [Fact]
    public void BadKeybindLines_AreReportedAtTheirLineAndSkipped()
    {
        var result = ConfigParser.Parse("keybind = f9=new_window\nkeybind = nonsense\nkeybind = ctrl+q=new_tab");

        Assert.Equal([1, 2], result.Diagnostics.Select(d => d.Line));
        Assert.Equal(TerminalAction.NewTab, result.Bindings[new KeyTrigger(TerminalKey.Q, KeyModifiers.Control)]);
    }

    [Fact]
    public void ConflictsBetweenUserLines_AreReportedInLineOrder()
    {
        var result = ConfigParser.Parse("font-size = bad\nkeybind = f9=new_tab\nkeybind = f9=next_tab");

        Assert.Equal([1, 3], result.Diagnostics.Select(d => d.Line));
    }

    [Fact]
    public void ALoadedFile_CarriesItsBindings()
    {
        var path = Path.Combine(Path.GetTempPath(), "wintty-keybinds-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, "keybind = f9=scroll_to_top");
        try
        {
            var result = ConfigLoader.Load(path);

            Assert.Equal(TerminalAction.ScrollToTop, result.Bindings[new KeyTrigger(TerminalKey.F9, KeyModifiers.None)]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WithoutAConfig_TheDefaultsApply()
    {
        var result = ConfigLoader.Load(Path.Combine(Path.GetTempPath(), "wintty-missing-" + Guid.NewGuid().ToString("N")));

        Assert.Equal(Keybinds.Defaults.Count, result.Bindings.Count);
    }
}
