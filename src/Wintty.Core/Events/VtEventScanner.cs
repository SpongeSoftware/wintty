using System.Text;

namespace Wintty.Core.Events;

/// <summary>A complete OSC (operating system command) sequence a program wrote to the terminal.</summary>
/// <param name="Command">The number after <c>ESC ]</c>, for example 7 for a working-directory report.</param>
/// <param name="Payload">Everything after the number and its <c>;</c>, decoded as UTF-8.</param>
public readonly record struct OscSequence(int Command, string Payload);

/// <summary>
/// Watches the byte stream a program writes for the few sequences that ask the terminal to act on the desktop:
/// OSC commands (working directory, notifications, clipboard, progress) and the bell. It only observes; the
/// terminal engine still receives every byte. State is kept between calls, so a sequence split across two reads
/// is still found.
/// </summary>
public sealed class VtEventScanner
{
    /// <summary>The longest OSC payload that is kept; longer ones are dropped so a runaway program cannot grow memory.</summary>
    public const int MaxPayloadBytes = 1 << 20;

    private enum State
    {
        Ground,
        Escape,
        OscNumber,
        OscPayload,
        OscEscape,
        OtherString,
        OtherStringEscape,
    }

    private readonly Action<OscSequence> _onOsc;
    private readonly Action _onBell;
    private readonly List<byte> _buffer = [];
    private State _state = State.Ground;
    private bool _overflowed;

    /// <summary>Creates a scanner.</summary>
    /// <param name="onOsc">Called for every complete OSC sequence.</param>
    /// <param name="onBell">Called for every bell character that is not the end of a sequence.</param>
    public VtEventScanner(Action<OscSequence> onOsc, Action onBell)
    {
        ArgumentNullException.ThrowIfNull(onOsc);
        ArgumentNullException.ThrowIfNull(onBell);
        _onOsc = onOsc;
        _onBell = onBell;
    }

    /// <summary>Scans the next stretch of program output.</summary>
    /// <param name="data">The bytes, in order.</param>
    public void Feed(ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            Step(value);
        }
    }

    private void Step(byte value)
    {
        switch (_state)
        {
            case State.Ground:
                if (value == 0x1b)
                {
                    _state = State.Escape;
                }
                else if (value == 0x07)
                {
                    _onBell();
                }

                break;
            case State.Escape:
                EnterFromEscape(value);
                break;
            case State.OscNumber:
            case State.OscPayload:
                ContinueOsc(value);
                break;
            case State.OscEscape:
                if (value == (byte)'\\')
                {
                    FinishOsc();
                }
                else
                {
                    // ESC inside an OSC cancels it and starts a new sequence.
                    ResetBuffer();
                    _state = State.Escape;
                    EnterFromEscape(value);
                }

                break;
            case State.OtherString:
                if (value == 0x1b)
                {
                    _state = State.OtherStringEscape;
                }

                break;
            case State.OtherStringEscape:
                _state = value == (byte)'\\' ? State.Ground : State.OtherString;
                break;
        }
    }

    private void EnterFromEscape(byte value)
    {
        switch (value)
        {
            case (byte)']':
                ResetBuffer();
                _state = State.OscNumber;
                break;
            case (byte)'P':
            case (byte)'_':
            case (byte)'^':
            case (byte)'X':
                // DCS, APC, PM and SOS strings run to ST; a bell inside one is data, not a bell.
                _state = State.OtherString;
                break;
            case 0x1b:
                _state = State.Escape;
                break;
            default:
                _state = State.Ground;
                break;
        }
    }

    private void ContinueOsc(byte value)
    {
        if (value == 0x07)
        {
            FinishOsc();
            return;
        }

        if (value == 0x1b)
        {
            _state = State.OscEscape;
            return;
        }

        if (_state == State.OscNumber && value == (byte)';')
        {
            _state = State.OscPayload;
        }

        if (_buffer.Count >= MaxPayloadBytes)
        {
            _overflowed = true;
            return;
        }

        _buffer.Add(value);
    }

    private void FinishOsc()
    {
        _state = State.Ground;
        var bytes = _buffer.ToArray();
        var overflowed = _overflowed;
        ResetBuffer();
        if (overflowed)
        {
            return;
        }

        var separator = Array.IndexOf(bytes, (byte)';');
        var numberBytes = separator < 0 ? bytes : bytes[..separator];
        if (!int.TryParse(Encoding.ASCII.GetString(numberBytes), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var command))
        {
            return;
        }

        var payload = separator < 0 ? string.Empty : Encoding.UTF8.GetString(bytes, separator + 1, bytes.Length - separator - 1);
        _onOsc(new OscSequence(command, payload));
    }

    private void ResetBuffer()
    {
        _buffer.Clear();
        _overflowed = false;
    }
}
