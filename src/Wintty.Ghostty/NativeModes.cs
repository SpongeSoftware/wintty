using System.Runtime.InteropServices;

namespace Wintty.Ghostty;

/// <summary>
/// Reads terminal modes. The <c>LibGhostty.Net</c> wrapper does not expose <c>ghostty_terminal_mode_get</c> yet,
/// so it is bound directly from the same native library. When the export cannot be found every mode reads as off.
/// </summary>
internal static unsafe class NativeModes
{
    /// <summary>DEC private mode 2004: the program asked for pasted text to be wrapped in markers.</summary>
    internal const ushort BracketedPaste = 2004;

    /// <summary>DEC private mode 2026: synchronised output.</summary>
    internal const ushort SynchronizedOutput = 2026;

    /// <summary>DEC private mode 1004: focus in and out reports.</summary>
    internal const ushort FocusEvents = 1004;

    /// <summary>DEC private mode 2031: colour scheme change notifications.</summary>
    internal const ushort ColorSchemeNotifications = 2031;

    private const string LibraryName = "ghostty-vt.dll";

    private static readonly Lazy<nint> ModeGet = new(Resolve);

    /// <summary>Reads a mode.</summary>
    /// <param name="terminal">Native terminal handle.</param>
    /// <param name="mode">The mode number; DEC private modes are used as is.</param>
    /// <returns><see langword="true"/> when the mode is set.</returns>
    internal static bool IsSet(IntPtr terminal, ushort mode)
    {
        if (ModeGet.Value == 0)
        {
            return false;
        }

        byte value = 0;
        var status = ((delegate* unmanaged[Cdecl]<IntPtr, ushort, byte*, int>)ModeGet.Value)(terminal, mode, &value);
        return status == 0 && value != 0;
    }

    private static nint Resolve()
    {
        var directories = new[] { Environment.GetEnvironmentVariable(NativeAssetLocator.OverrideVariable), AppContext.BaseDirectory };
        foreach (var directory in directories.Where(d => !string.IsNullOrEmpty(d)))
        {
            // Loading an already-loaded library returns the same handle, so this shares the wrapper copy.
            if (NativeLibrary.TryLoad(Path.Combine(directory!, LibraryName), out var library)
                && NativeLibrary.TryGetExport(library, "ghostty_terminal_mode_get", out var export))
            {
                return export;
            }
        }

        return 0;
    }
}
