using Callora.Core.Extensibility;
namespace Callora.Core.Application.Diagnostics;

/// <summary>
/// Marks which plugin's code is executing on the current asynchronous flow, so work the
/// plugin causes further down — database commands above all — can be attributed to it.
/// </summary>
/// <remarks>
/// <para>
/// Under ADR-013 several foreign plugins share one process and one database connection.
/// Nothing about a query says who issued it, and walking the stack to find out would be
/// both expensive and unreliable across an <c>await</c>. So the host marks the scope where
/// it hands control to a plugin, which it already identifies for the availability gate.
/// </para>
/// <para>
/// <see cref="AsyncLocal{T}"/> rather than a field: the value has to follow one request
/// across awaits and thread-pool hops without leaking into requests running beside it.
/// </para>
/// </remarks>
[CalloraInternal("Attribution for the execution recorder — writing it would let a plugin file its work under a neighbour (REV2 §7.2)")]
public static class PluginExecutionScope
{
    private static readonly AsyncLocal<string?> CurrentPluginId = new();

    /// <summary>The plugin whose code is executing, or null for host work.</summary>
    public static string? Current => CurrentPluginId.Value;

    /// <summary>
    /// Marks the current flow as belonging to <paramref name="pluginId"/> until the returned
    /// handle is disposed, restoring whatever was in effect before.
    /// </summary>
    public static IDisposable Enter(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var previous = CurrentPluginId.Value;
        CurrentPluginId.Value = pluginId;
        return new PluginExecutionScopeHandle(previous);
    }

    /// <summary>Restores a previously captured value. Used by the handle.</summary>
    internal static void Restore(string? pluginId) => CurrentPluginId.Value = pluginId;
}
