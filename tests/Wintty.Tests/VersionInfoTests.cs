using Wintty.Core;
using Xunit;

namespace Wintty.Tests;

public class VersionInfoTests
{
    [Fact]
    public void Current_IsNotEmptyAndHasNoBuildMetadata()
    {
        Assert.False(string.IsNullOrWhiteSpace(VersionInfo.Current));
        Assert.DoesNotContain('+', VersionInfo.Current);
    }
}
