using System.Runtime.InteropServices;

namespace Wintty.Ghostty;

/// <summary>
/// Points <c>LibGhostty.Net</c> at the packaged native assets. In builds without a runtime identifier
/// (dev and test runs) the package leaves them under <c>runtimes/&lt;rid&gt;/native</c>, where its own
/// default search does not look; self-contained publishes flatten them next to the executable.
/// </summary>
internal static class NativeAssetLocator
{
    internal const string OverrideVariable = "LIBGHOSTTY_NATIVE_DIR";

    /// <summary>Sets <see cref="OverrideVariable"/> when it is unset and a packaged directory exists.</summary>
    /// <param name="baseDirectory">Directory containing the application binaries.</param>
    /// <param name="architecture">Process architecture used to pick the runtime folder.</param>
    /// <returns>The directory that was configured, or <see langword="null"/> if nothing was changed.</returns>
    internal static string? Configure(string baseDirectory, Architecture architecture)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OverrideVariable)))
        {
            return null;
        }

        string candidate = Path.Combine(baseDirectory, "runtimes", RuntimeIdentifier(architecture), "native");
        if (!Directory.Exists(candidate))
        {
            return null;
        }

        Environment.SetEnvironmentVariable(OverrideVariable, candidate);
        return candidate;
    }

    private static string RuntimeIdentifier(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => "win-arm64",
        _ => "win-x64",
    };
}
