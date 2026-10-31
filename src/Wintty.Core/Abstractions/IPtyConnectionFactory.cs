namespace Wintty.Core.Abstractions;

/// <summary>Starts processes inside a pseudo-terminal.</summary>
public interface IPtyConnectionFactory
{
    /// <summary>Launches a process attached to a new pseudo-terminal.</summary>
    /// <param name="options">What to run and the initial size.</param>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>The running connection; the caller must dispose it.</returns>
    Task<IPtyConnection> StartAsync(PtyStartOptions options, CancellationToken cancellationToken = default);
}
