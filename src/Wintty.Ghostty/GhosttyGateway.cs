using System.Runtime.InteropServices;
using LibGhostty;
using Wintty.Core.Abstractions;

namespace Wintty.Ghostty;

/// <summary>
/// The single entry point to libghostty / the <c>LibGhostty.Net</c> package. It implements the Core factories
/// so the rest of the application never references the native binding directly.
/// </summary>
public sealed class GhosttyGateway : ITerminalEngineFactory, IPtyConnectionFactory
{
    private static readonly Lazy<GhosttyNativeLibrary> Native = new(() =>
    {
        NativeAssetLocator.Configure(AppContext.BaseDirectory, RuntimeInformation.ProcessArchitecture);
        return GhosttyNativeLibrary.LoadFromPackage();
    });

    static GhosttyGateway()
    {
        // The PTY host resolves ConPTY/OpenConsole from the same location, so configure it before first use.
        NativeAssetLocator.Configure(AppContext.BaseDirectory, RuntimeInformation.ProcessArchitecture);
    }

    /// <inheritdoc />
    public ITerminalEngine Create(TerminalEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return new GhosttyTerminalEngine(Native.Value, options);
    }

    /// <inheritdoc />
    public async Task<IPtyConnection> StartAsync(PtyStartOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var native = new GhosttyPtyOptions
        {
            Name = "wintty",
            Cols = options.Columns,
            Rows = options.Rows,
            Cwd = options.WorkingDirectory ?? Environment.CurrentDirectory,
            App = options.Application,
            CommandLine = options.Arguments,
            Environment = options.Environment ?? new Dictionary<string, string>(),
        };

        IGhosttyPtyConnection connection = await GhosttyPtyConnectionFactory.StartAsync(native, cancellationToken).ConfigureAwait(false);
        return new GhosttyPtyConnection(connection);
    }
}
