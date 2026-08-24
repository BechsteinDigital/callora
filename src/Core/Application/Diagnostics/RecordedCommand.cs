using Callora.Core.Extensibility;
namespace Callora.Core.Application.Diagnostics;

/// <summary>
/// One database command, with the plugin it is attributed to.
/// </summary>
/// <param name="PluginId">
/// The plugin whose code was executing, or null for host work. This is the field the whole
/// recorder exists for — under ADR-013 several foreign plugins share one process and one
/// connection, so "which plugin issued this" is the question nothing else can answer.
/// </param>
/// <param name="CommandText">The SQL as sent.</param>
/// <param name="Duration">How long it took.</param>
/// <param name="OccurredAtUtc">When it started.</param>
[CalloraInternal("Operator diagnostics — not a plugin contract (REV2 §7.2)")]
public sealed record RecordedCommand(
    string? PluginId,
    string CommandText,
    TimeSpan Duration,
    DateTimeOffset OccurredAtUtc);
