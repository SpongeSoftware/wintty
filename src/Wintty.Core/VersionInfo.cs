using System.Reflection;

namespace Wintty.Core;

/// <summary>Exposes the build version, which is injected from the git tag (-p:Version=x.y.z).</summary>
public static class VersionInfo
{
    /// <summary>The semantic version of this build, without build metadata (for example <c>1.2.3</c>).</summary>
    public static string Current { get; } = Resolve(typeof(VersionInfo).Assembly);

    internal static string Resolve(Assembly assembly)
    {
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        // Strip build metadata such as the source revision appended by the SDK ("1.2.3+abcdef").
        int plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
