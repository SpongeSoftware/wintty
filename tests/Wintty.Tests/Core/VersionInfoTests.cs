using System.Reflection;
using System.Reflection.Emit;
using Wintty.Core;
using Xunit;

namespace Wintty.Tests.Core;

public class VersionInfoTests
{
    [Fact]
    public void Current_IsNotEmptyAndHasNoBuildMetadata()
    {
        Assert.False(string.IsNullOrWhiteSpace(VersionInfo.Current));
        Assert.DoesNotContain('+', VersionInfo.Current);
    }

    [Fact]
    public void Resolve_StripsBuildMetadata() =>
        Assert.Equal("1.2.3-rc.1", VersionInfo.Resolve(BuildAssembly("1.0.0.0", "1.2.3-rc.1+abc123")));

    [Fact]
    public void Resolve_KeepsPlainInformationalVersion() =>
        Assert.Equal("4.5.6", VersionInfo.Resolve(BuildAssembly("1.0.0.0", "4.5.6")));

    [Fact]
    public void Resolve_FallsBackToAssemblyVersionWithoutInformationalVersion() =>
        Assert.Equal("7.8.9", VersionInfo.Resolve(BuildAssembly("7.8.9.0", informational: null)));

    private static Assembly BuildAssembly(string version, string? informational)
    {
        var name = new AssemblyName("Dyn" + Guid.NewGuid().ToString("N")) { Version = Version.Parse(version) };
        var builder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        if (informational is not null)
        {
            var ctor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            builder.SetCustomAttribute(new CustomAttributeBuilder(ctor, [informational]));
        }

        return builder;
    }
}
