using System.Diagnostics;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Wraps lifecycle operations with telemetry activities, timing and correlation.
/// </summary>
public static class PluginLifecycleInstrumentation
{
    /// <summary>
    /// Executes one lifecycle operation inside a telemetry scope.
    /// </summary>
    public static async Task<PluginLifecycleServiceResult> ExecuteAsync(
        string action,
        string? pluginId,
        string? requestedBy,
        string? workspaceKey,
        Func<CancellationToken, Task<PluginLifecycleServiceResult>> executeAsync,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var scope = string.IsNullOrWhiteSpace(workspaceKey) ? "global" : "workspace";
        using var activity = PluginLifecycleTelemetry.StartOperationActivity(action, pluginId, requestedBy, workspaceKey, scope);

        PluginLifecycleServiceResult? result = null;
        try
        {
            result = await executeAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception exception)
        {
            PluginLifecycleTelemetry.RecordException(activity, exception);
            throw;
        }
        finally
        {
            PluginLifecycleTelemetry.CompleteOperation(
                action,
                scope,
                result?.PluginId ?? pluginId,
                activity,
                result?.IsSuccess ?? false,
                result?.ErrorCode,
                startedAt);
        }
    }
}
