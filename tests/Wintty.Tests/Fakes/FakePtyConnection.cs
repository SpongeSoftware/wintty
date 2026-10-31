using System.IO.Pipelines;
using Wintty.Core.Abstractions;

namespace Wintty.Tests.Fakes;

/// <summary>Scriptable <see cref="IPtyConnection"/>: tests push "process output" and inspect input and calls.</summary>
internal sealed class FakePtyConnection : IPtyConnection
{
    private readonly Pipe _pipe = new();

    public FakePtyConnection()
    {
        Output = _pipe.Reader.AsStream();
    }

    public event EventHandler<int>? Exited;

    public Stream Output { get; }

    public MemoryStream InputBuffer { get; } = new();

    public Stream? InputOverride { get; set; }

    public Stream Input => InputOverride ?? InputBuffer;

    public int ProcessId => 1234;

    public (int Columns, int Rows)? LastResize { get; private set; }

    public bool Killed { get; private set; }

    public bool Disposed { get; private set; }

    /// <summary>Simulates the process writing to the terminal.</summary>
    public async Task EmitAsync(string text)
    {
        await _pipe.Writer.WriteAsync(System.Text.Encoding.UTF8.GetBytes(text));
    }

    /// <summary>Simulates the process closing its output.</summary>
    public async Task CloseOutputAsync() => await _pipe.Writer.CompleteAsync();

    public void RaiseExited(int code) => Exited?.Invoke(this, code);

    public void Resize(int columns, int rows) => LastResize = (columns, rows);

    public void Kill() => Killed = true;

    public void Dispose()
    {
        Disposed = true;
        _pipe.Writer.Complete();
        _pipe.Reader.Complete();
    }
}

/// <summary>Factory returning a pre-built <see cref="FakePtyConnection"/>, or throwing on request.</summary>
internal sealed class FakePtyConnectionFactory : IPtyConnectionFactory
{
    public FakePtyConnection Connection { get; } = new();

    public Exception? FailWith { get; set; }

    public PtyStartOptions? LastOptions { get; private set; }

    public Task<IPtyConnection> StartAsync(PtyStartOptions options, CancellationToken cancellationToken = default)
    {
        LastOptions = options;
        if (FailWith is not null)
        {
            return Task.FromException<IPtyConnection>(FailWith);
        }

        return Task.FromResult<IPtyConnection>(Connection);
    }
}

/// <summary>A stream whose writes always fail, to simulate a closed pseudo-terminal.</summary>
internal sealed class ThrowingStream : Stream
{
    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new IOException("closed");

    public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("closed");
}
