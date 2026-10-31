namespace Wintty.Core.Abstractions;

/// <summary>A running process attached to a pseudo-terminal.</summary>
public interface IPtyConnection : IDisposable
{
    /// <summary>Raised when the process exits, with its exit code.</summary>
    event EventHandler<int>? Exited;

    /// <summary>Stream of bytes written by the process.</summary>
    Stream Output { get; }

    /// <summary>Stream used to send input bytes to the process.</summary>
    Stream Input { get; }

    /// <summary>Operating-system process id.</summary>
    int ProcessId { get; }

    /// <summary>Informs the pseudo-terminal of a new size.</summary>
    /// <param name="columns">New width in cells.</param>
    /// <param name="rows">New height in cells.</param>
    void Resize(int columns, int rows);

    /// <summary>Terminates the process.</summary>
    void Kill();
}
