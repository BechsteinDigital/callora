using System.Text;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// One entry being collected from a call's tones. Ends exactly once, whichever way it ends.
/// </summary>
/// <remarks>
/// <para><b>Duplicates.</b> The transport reports the same keypress more than once — in-band echo, RFC
/// 4733 retransmissions — and the tone contract deliberately does not de-bounce. Two identical tones
/// within the duplicate window are therefore treated as one press. That window is what separates an
/// echo from somebody deliberately pressing the same key twice, which is far slower.</para>
/// <para><b>Threads.</b> Tones arrive from signalling and from the media path with no ordering promise,
/// so every access to the buffer is locked and the handler returns immediately — it runs on the path
/// that raised it, and blocking there stalls that path for every other call on the line.</para>
/// </remarks>
internal sealed class DtmfCollection
{
    private readonly ICall _call;
    private readonly DtmfCollectOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationToken _cancellationToken;
    private readonly TaskCompletionSource<DtmfEntry> _entry =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StringBuilder _digits = new();
    private readonly object _gate = new();

    private ITimer? _pauseTimer;
    private CancellationTokenRegistration _cancellation;
    private Action? _onFinished;
    private char? _lastTone;
    private DateTimeOffset _lastToneAt;
    private bool _finished;

    /// <summary>Prepares the collection; nothing is observed until <see cref="Start"/>.</summary>
    public DtmfCollection(
        ICall call,
        DtmfCollectOptions options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        _call = call;
        _options = options;
        _timeProvider = timeProvider;
        _cancellationToken = cancellationToken;
    }

    /// <summary>The entry, once the collection ends.</summary>
    public Task<DtmfEntry> Entry => _entry.Task;

    /// <summary>Begins observing the call; <paramref name="onFinished"/> runs once, however it ends.</summary>
    public void Start(Action onFinished)
    {
        _onFinished = onFinished;

        _call.DtmfReceived += OnDtmfReceived;
        _call.StateChanged += OnStateChanged;
        _cancellation = _cancellationToken.Register(Supersede);

        _lastToneAt = _timeProvider.GetUtcNow();
        _pauseTimer = _timeProvider.CreateTimer(
            _ => OnPauseElapsed(), state: null, _options.InterDigitTimeout, _options.InterDigitTimeout);

        // A call that ended between resolving it and subscribing would otherwise never end this
        // collection: no further state change is coming.
        if (_call.State == CallState.Terminated)
        {
            Finish(new DtmfEntry(DtmfEntryOutcome.CallEnded, null));
        }
    }

    /// <summary>Ends the collection because something else took over.</summary>
    public void Supersede() => Finish(new DtmfEntry(DtmfEntryOutcome.Superseded, null));

    private void OnDtmfReceived(object? sender, DtmfReceivedEventArgs e)
    {
        DtmfEntry? completed = null;

        lock (_gate)
        {
            if (_finished || IsEchoOfTheLastTone(e.Tone))
            {
                return;
            }

            _lastTone = e.Tone;
            _lastToneAt = _timeProvider.GetUtcNow();

            if (e.Tone == _options.ClearKey)
            {
                completed = new DtmfEntry(DtmfEntryOutcome.Cleared, null);
            }
            else if (e.Tone == _options.SubmitKey)
            {
                completed = new DtmfEntry(DtmfEntryOutcome.Completed, _digits.ToString());
            }
            else if (e.Tone is >= '0' and <= '9')
            {
                _digits.Append(e.Tone);
                if (_digits.Length >= _options.Length)
                {
                    completed = new DtmfEntry(DtmfEntryOutcome.Completed, _digits.ToString());
                }
            }

            // Anything else — the A–D keys almost no handset has — is ignored rather than treated as
            // an error, but it counts as activity so a caller pressing them is not timed out.
        }

        if (completed is not null)
        {
            Finish(completed);
        }
    }

    // Must be called under _gate.
    private bool IsEchoOfTheLastTone(char tone) =>
        _lastTone == tone &&
        _timeProvider.GetUtcNow() - _lastToneAt < _options.EffectiveDuplicateWindow;

    private void OnStateChanged(object? sender, CallStateChangedEventArgs e)
    {
        if (e.CurrentState == CallState.Terminated)
        {
            Finish(new DtmfEntry(DtmfEntryOutcome.CallEnded, null));
        }
    }

    private void OnPauseElapsed()
    {
        lock (_gate)
        {
            if (_finished || _timeProvider.GetUtcNow() - _lastToneAt < _options.InterDigitTimeout)
            {
                return;
            }
        }

        Finish(new DtmfEntry(DtmfEntryOutcome.TimedOut, null));
    }

    private void Finish(DtmfEntry entry)
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
        }

        _call.DtmfReceived -= OnDtmfReceived;
        _call.StateChanged -= OnStateChanged;
        _cancellation.Dispose();
        _pauseTimer?.Dispose();
        _onFinished?.Invoke();

        // The digits travel in the result and nowhere else — never into a log line or an exception
        // message. A PIN typed into a phone is a bearer secret with a very small alphabet.
        _entry.TrySetResult(entry);
    }
}
