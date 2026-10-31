using Wintty.Core.Abstractions;
using Xunit;

namespace Wintty.Tests.Core;

public class OptionsTests
{
    [Fact]
    public void TerminalEngineOptions_AcceptsValidValues() => new TerminalEngineOptions(80, 24).Validate();

    [Theory]
    [InlineData(0, 24, 0)]
    [InlineData(80, 0, 0)]
    [InlineData(80, 24, -1)]
    public void TerminalEngineOptions_RejectsInvalidValues(int columns, int rows, int scrollback) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerminalEngineOptions(columns, rows, scrollback).Validate());

    [Fact]
    public void PtyStartOptions_AcceptsValidValuesAndHasDefaults()
    {
        var options = new PtyStartOptions("cmd.exe", 80, 24);
        options.Validate();

        Assert.Empty(options.Arguments);
        Assert.Null(options.WorkingDirectory);
        Assert.Null(options.Environment);
    }

    [Fact]
    public void PtyStartOptions_RejectsBlankApplication() =>
        Assert.Throws<ArgumentException>(() => new PtyStartOptions(" ", 80, 24).Validate());

    [Theory]
    [InlineData(0, 24)]
    [InlineData(80, 0)]
    public void PtyStartOptions_RejectsInvalidSize(int columns, int rows) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PtyStartOptions("cmd.exe", columns, rows).Validate());
}
