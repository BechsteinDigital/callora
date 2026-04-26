using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Callora.Host.Backend.Application.Lifecycle;

public static class PluginLifecycleTelemetry
{
    public const string ActivitySourceName = "Callora.Host.Backend.PluginLifecycle";
    public const string MeterName = "Callora.Host.Backend.PluginLifecycle";
    public const string OperationCountMetricName = "callora.plugin.lifecycle.operations";
    public const string DurationMetricName = "callora.plugin.lifecycle.duration.ms";

    private static readonly ActivitySource LifecycleActivitySource = new(ActivitySourceName);
    private static readonly Meter LifecycleMeter = new(MeterName);
    private static readonly Counter<long> OperationCounter = LifecycleMeter.CreateCounter<long>(
        OperationCountMetricName,
        unit: "operation",
        description: "Counts plugin lifecycle operations by action/outcome.");
    private static readonly Histogram<double> OperationDurationMs = LifecycleMeter.CreateHistogram<double>(
        DurationMetricName,
        unit: "ms",
        description: "Plugin lifecycle operation duration in milliseconds.");

    public static Activity? StartOperationActivity(
        string action,
        string? pluginId,
        string? requestedBy,
        string? workspaceKey,
        string scope)
    {
        var activity = LifecycleActivitySource.StartActivity($"plugin.lifecycle.{action}", ActivityKind.Internal);
        if (activity is null)
            return null;

        activity.SetTag("plugin.lifecycle.action", action);
        activity.SetTag("plugin.lifecycle.scope", scope);
        activity.SetTag("plugin.id", pluginId);
        activity.SetTag("requested.by", requestedBy);
        activity.SetTag("workspace.key", workspaceKey);
        activity.SetTag("correlation.id", activity.TraceId.ToString());

        return activity;
    }

    public static void CompleteOperation(
        string action,
        string scope,
        Activity? activity,
        bool isSuccess,
        string? errorCode,
        long startTimestamp)
    {
        var outcome = isSuccess ? "success" : "failure";
        var correlationId = GetCurrentCorrelationId(activity);

        activity?.SetTag("plugin.lifecycle.outcome", outcome);
        activity?.SetTag("plugin.lifecycle.error_code", errorCode);
        activity?.SetStatus(isSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error, errorCode);

        TagList tags =
        [
            new KeyValuePair<string, object?>("plugin.lifecycle.action", action),
            new KeyValuePair<string, object?>("plugin.lifecycle.scope", scope),
            new KeyValuePair<string, object?>("plugin.lifecycle.outcome", outcome),
            new KeyValuePair<string, object?>("correlation.id", correlationId)
        ];

        OperationCounter.Add(1, tags);
        OperationDurationMs.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds, tags);
    }

    public static void RecordException(Activity? activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("plugin.lifecycle.exception", exception.GetType().FullName);
    }

    public static string GetCurrentCorrelationId(Activity? activity = null)
    {
        var effectiveActivity = activity ?? Activity.Current;
        return effectiveActivity?.TraceId.ToString() ?? string.Empty;
    }
}
