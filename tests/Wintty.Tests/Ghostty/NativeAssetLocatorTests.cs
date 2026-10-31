using System.Runtime.InteropServices;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Mutates a process-wide environment variable, so it must not run in parallel with other tests.</summary>
[Collection("NativeEnvironment")]
public sealed class NativeAssetLocatorTests : IDisposable
{
    private const string Variable = NativeAssetLocator.OverrideVariable;
    private readonly string? _original = Environment.GetEnvironmentVariable(Variable);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wintty-locator-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Variable, _original);
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Configure_DoesNothingWhenOverrideAlreadySet()
    {
        Environment.SetEnvironmentVariable(Variable, "C:\\custom");

        Assert.Null(NativeAssetLocator.Configure(_root, Architecture.X64));
        Assert.Equal("C:\\custom", Environment.GetEnvironmentVariable(Variable));
    }

    [Fact]
    public void Configure_DoesNothingWhenPackagedDirectoryMissing()
    {
        Environment.SetEnvironmentVariable(Variable, null);

        Assert.Null(NativeAssetLocator.Configure(_root, Architecture.X64));
        Assert.Null(Environment.GetEnvironmentVariable(Variable));
    }

    [Theory]
    [InlineData(Architecture.X64, "win-x64")]
    [InlineData(Architecture.Arm64, "win-arm64")]
    [InlineData(Architecture.X86, "win-x64")]
    public void Configure_SetsVariableToRuntimeSpecificDirectory(Architecture architecture, string rid)
    {
        Environment.SetEnvironmentVariable(Variable, null);
        var expected = Path.Combine(_root, "runtimes", rid, "native");
        Directory.CreateDirectory(expected);

        Assert.Equal(expected, NativeAssetLocator.Configure(_root, architecture));
        Assert.Equal(expected, Environment.GetEnvironmentVariable(Variable));
    }
}
