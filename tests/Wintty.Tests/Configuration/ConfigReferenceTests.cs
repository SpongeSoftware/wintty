using System.Text.RegularExpressions;
using Wintty.Core.Configuration;
using Xunit;

namespace Wintty.Tests.Configuration;

/// <summary>
/// Keeps the generated option reference honest: every option the parser handles is documented, every documented
/// option is handled, defaults match the app, and the page on the docs site equals what the code renders.
/// Set <c>WINTTY_UPDATE_DOCS=1</c> and run the tests to rewrite the page instead of failing.
/// </summary>
public partial class ConfigReferenceTests
{
    private const string Begin = "<!-- BEGIN GENERATED OPTIONS -->";
    private const string End = "<!-- END GENERATED OPTIONS -->";
    private const string KeysBegin = "<!-- BEGIN GENERATED KEYBINDINGS -->";
    private const string KeysEnd = "<!-- END GENERATED KEYBINDINGS -->";

    [GeneratedRegex("case \"([a-z-]+)\":")]
    private static partial Regex CaseLabel();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "wintty.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void EveryOptionTheParserHandlesIsDocumentedAndViceVersa()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Wintty.Core", "Configuration", "ConfigParser.cs"));
        var handled = CaseLabel().Matches(source).Select(m => m.Groups[1].Value)
            .Where(name => name is not ("true" or "false")).ToHashSet();

        var documented = ConfigOptions.All.Select(o => o.Name).ToHashSet();

        Assert.Empty(handled.Except(documented));
        Assert.Empty(documented.Except(handled));
        Assert.Equal(ConfigOptions.All.Count, documented.Count);
    }

    [Fact]
    public void EveryOptionHasADescriptionAndADefault()
    {
        foreach (var option in ConfigOptions.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(option.Description), option.Name);
            Assert.False(string.IsNullOrWhiteSpace(option.Default), option.Name);
        }
    }

    [Fact]
    public void DocumentedDefaultsMatchTheApp()
    {
        var defaults = new WinttyConfig();
        string Default(string name) => ConfigOptions.All.Single(o => o.Name == name).Default.Trim('`');

        Assert.Equal(defaults.FontFamily, Default("font-family"));
        Assert.Equal(defaults.FontSize, float.Parse(Default("font-size"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(defaults.WindowPadding, double.Parse(Default("window-padding"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(defaults.ScrollbackLimit, int.Parse(Default("scrollback-limit"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(defaults.CursorBlink, bool.Parse(Default("cursor-style-blink")));
    }

    [Fact]
    public void EveryOptionNameIsAcceptedByTheParser()
    {
        foreach (var option in ConfigOptions.All)
        {
            var result = ConfigParser.Parse(option.Name + " =");

            Assert.Empty(result.Diagnostics);
        }
    }

    [Fact]
    public void RenderTable_HasOneRowPerOption()
    {
        var table = ConfigOptions.RenderTable();

        Assert.Equal(ConfigOptions.All.Count + 2, table.TrimEnd().Split('\n').Length);
        Assert.StartsWith("| Option | Default | Applies | Description |", table, StringComparison.Ordinal);
    }

    [Fact]
    public void KeybindingsPageMatchesTheGeneratedReference()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "src", "content", "docs", "keybindings.md");
        var page = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = page.IndexOf(KeysBegin, StringComparison.Ordinal);
        var finish = page.IndexOf(KeysEnd, StringComparison.Ordinal);
        Assert.True(start >= 0 && finish > start, "keybindings.md needs the generated-keybindings markers.");

        var expected = page[..(start + KeysBegin.Length)] + "\n" + Wintty.Core.Keybindings.Keybinds.RenderReference() + page[finish..];
        if (expected == page)
        {
            return;
        }

        if (Environment.GetEnvironmentVariable("WINTTY_UPDATE_DOCS") == "1")
        {
            File.WriteAllText(path, expected);
            return;
        }

        Assert.Fail("docs/src/content/docs/keybindings.md is out of date. Run the tests with WINTTY_UPDATE_DOCS=1 to regenerate it.");
    }

    [Fact]
    public void ConfigurationPageMatchesTheGeneratedReference()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "src", "content", "docs", "configuration.md");
        var page = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = page.IndexOf(Begin, StringComparison.Ordinal);
        var finish = page.IndexOf(End, StringComparison.Ordinal);
        Assert.True(start >= 0 && finish > start, "configuration.md needs the generated-options markers.");

        var expected = page[..(start + Begin.Length)] + "\n" + ConfigOptions.RenderTable() + page[finish..];
        if (expected == page)
        {
            return;
        }

        if (Environment.GetEnvironmentVariable("WINTTY_UPDATE_DOCS") == "1")
        {
            File.WriteAllText(path, expected);
            return;
        }

        Assert.Fail("docs/src/content/docs/configuration.md is out of date. Run the tests with WINTTY_UPDATE_DOCS=1 to regenerate it.");
    }
}
