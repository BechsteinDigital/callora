using Callora.Core.Extensibility;
namespace Callora.Core.Application.Diagnostics;

/// <summary>
/// Captures database commands with the plugin that issued them, for a bounded window.
/// </summary>
/// <remarks>
/// <para>
/// Everything else the host records is an aggregate — job telemetry, lifecycle telemetry,
/// SLO evaluation. Those answer "is the platform healthy". None of them answers "this one
/// request took four seconds, who spent it", and under ADR-013 (trusted in-process) that is
/// exactly the question an operator asks: several foreign plugins share one process and one
/// database connection.
/// </para>
/// <para>
/// Off by default, and it turns itself off — that is what makes it safe to enable on a
/// system that matters. The failure mode of a diagnostic tool is not being wrong, it is
/// being switched on during an incident and forgotten afterwards.
/// </para>
/// </remarks>
[CalloraInternal("Operator diagnostics — not a plugin contract (REV2 §7.2)")]
public sealed class PluginExecutionRecorder(TimeProvider timeProvider)
{
    /// <summary>The longest a recording run may last, however long the caller asks for.</summary>
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromMinutes(10);

    /// <summary>How many commands are kept before the oldest is dropped.</summary>
    public const int Capacity = 500;

    private readonly object _gate = new();
    private readonly Queue<RecordedCommand> _commands = new();
    private RecorderSession? _session;
    private DateTimeOffset _expiresAtUtc;

    /// <summary>Whether recording is currently on. False once the window has elapsed.</summary>
    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return ActiveSession() is not null;
            }
        }
    }

    /// <summary>Starts a recording run, replacing any run already in progress.</summary>
    public void Start(RecorderSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            var window = session.Window > MaximumWindow || session.Window <= TimeSpan.Zero
                ? MaximumWindow
                : session.Window;
            _session = session with { Window = window };
            _expiresAtUtc = timeProvider.GetUtcNow() + window;
            _commands.Clear();
        }
    }

    /// <summary>Stops recording now. What was captured stays readable.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _session = null;
        }
    }

    /// <summary>
    /// Records one command. A no-op while off — the cheap path, taken on every query of
    /// every request for the entire life of the host.
    /// </summary>
    public void RecordCommand(string? pluginId, string commandText, TimeSpan duration)
    {
        lock (_gate)
        {
            var session = ActiveSession();
            if (session is null)
            {
                return;
            }

            if (session.PluginId is not null &&
                !string.Equals(session.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _commands.Enqueue(new RecordedCommand(pluginId, commandText, duration, timeProvider.GetUtcNow()));
            while (_commands.Count > Capacity)
            {
                _commands.Dequeue();
            }
        }
    }

    /// <summary>The captured commands, oldest first.</summary>
    public IReadOnlyList<RecordedCommand> Recent()
    {
        lock (_gate)
        {
            return _commands.ToArray();
        }
    }

    // Expiry is evaluated on read rather than by a timer: a timer would have to be disposed,
    // and a recorder that keeps a live callback after its window is the thing this class
    // exists to avoid.
    private RecorderSession? ActiveSession()
    {
        if (_session is null)
        {
            return null;
        }

        if (timeProvider.GetUtcNow() >= _expiresAtUtc)
        {
            _session = null;
            return null;
        }

        return _session;
    }
}
