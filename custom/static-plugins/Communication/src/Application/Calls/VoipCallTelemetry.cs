using System.Diagnostics.Metrics;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Metrics for the live-call stack, emitted by the voice plugin (PLAT-257).
/// </summary>
public static class VoipCallTelemetry
{
    public const string MeterName = "Callora.Voip.Calls";

    private static readonly Meter CallMeter = new(MeterName);

    private static readonly UpDownCounter<long> ActiveCalls = CallMeter.CreateUpDownCounter<long>(
        "callora.calls.active",
        unit: "call",
        description: "Currently tracked live calls.");

    private static readonly Counter<long> StartedCalls = CallMeter.CreateCounter<long>(
        "callora.calls.started",
        unit: "call",
        description: "Started calls by direction.");

    private static readonly Histogram<double> CallDurationMs = CallMeter.CreateHistogram<double>(
        "callora.calls.duration",
        unit: "ms",
        description: "Duration of ended calls from tracking start to termination.");

    public static void RecordStarted(string workspaceKey, string direction)
    {
        var tags = BuildTags(workspaceKey, direction);
        ActiveCalls.Add(1, tags);
        StartedCalls.Add(1, tags);
    }

    public static void RecordEnded(string workspaceKey, string direction, TimeSpan duration)
    {
        var tags = BuildTags(workspaceKey, direction);
        ActiveCalls.Add(-1, tags);
        CallDurationMs.Record(duration.TotalMilliseconds, tags);
    }

    private static KeyValuePair<string, object?>[] BuildTags(string workspaceKey, string direction) =>
    [
        new("workspace.key", workspaceKey),
        new("call.direction", direction)
    ];
}
