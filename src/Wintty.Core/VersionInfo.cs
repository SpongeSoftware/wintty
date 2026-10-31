using System.Reflection;

namespace Wintty.Core;

/// <summary>Exposes the build version, which is injected from the git tag (-p:Version=x.y.z).</summary>
public static class VersionInfo
{
    public static string Current { get; } = Resolve(typeof(VersionInfo).Assembly);

    internal static string Resolve(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(informational))
        {
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        // Strip build metadata such as the source revision appended by the SDK ("1.2.3+abcdef").
        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
