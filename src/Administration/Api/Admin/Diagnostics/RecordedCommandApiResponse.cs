namespace Callora.Administration.Api.Admin.Diagnostics;

/// <summary>One captured database command.</summary>
/// <param name="PluginId">The plugin whose code was running, or null for host work.</param>
/// <param name="CommandText">The SQL as sent.</param>
/// <param name="DurationMs">How long it took.</param>
/// <param name="OccurredAtUtc">When it started.</param>
public sealed record RecordedCommandApiResponse(
    string? PluginId,
    string CommandText,
    double DurationMs,
    DateTimeOffset OccurredAtUtc);
