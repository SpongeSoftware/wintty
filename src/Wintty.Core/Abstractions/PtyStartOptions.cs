namespace Wintty.Core.Abstractions;

/// <summary>Describes the process to launch inside a pseudo-terminal.</summary>
/// <param name="Application">Path or name of the executable.</param>
/// <param name="Columns">Initial width in cells.</param>
/// <param name="Rows">Initial height in cells.</param>
public sealed record PtyStartOptions(string Application, int Columns, int Rows)
{
    /// <summary>Command-line arguments (excluding the executable).</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Working directory, or <see langword="null"/> for the current directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Extra environment variables for the child process.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Throws if the options are not usable.</summary>
    /// <exception cref="ArgumentException">The application is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Application);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Columns, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Rows, 0);
    }
}
