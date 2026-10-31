using Wintty.Core.Configuration;
using Xunit;

namespace Wintty.Tests.Configuration;

public class CommandLineTests
{
    [Fact]
    public void Split_RejectsNull() => Assert.Throws<ArgumentNullException>(() => CommandLine.Split(null!));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Split_BlankIsNull(string text) => Assert.Null(CommandLine.Split(text));

    [Fact]
    public void Split_SeparatesExecutableAndArguments()
    {
        var (application, arguments) = CommandLine.Split("pwsh.exe -NoLogo   -NoProfile")!.Value;

        Assert.Equal("pwsh.exe", application);
        Assert.Equal(["-NoLogo", "-NoProfile"], arguments);
    }

    [Fact]
    public void Split_QuotesGroupWordsAndAreRemoved()
    {
        var (application, arguments) = CommandLine.Split("\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\" -Command \"echo hi\"")!.Value;

        Assert.Equal("C:\\Program Files\\PowerShell\\7\\pwsh.exe", application);
        Assert.Equal(["-Command", "echo hi"], arguments);
    }

    [Fact]
    public void Split_EmptyQuotesProduceAnEmptyArgument()
    {
        Assert.Equal([string.Empty], CommandLine.Split("tool \"\"")!.Value.Arguments);
    }
}
