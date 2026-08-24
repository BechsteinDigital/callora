namespace Callora.Administration.Api.Admin.Diagnostics;

/// <summary>Body of a recorder start request.</summary>
/// <param name="WindowSeconds">
/// How long to record. Clamped to the recorder's ceiling; omitted means the ceiling.
/// </param>
/// <param name="PluginId">
/// Record only this plugin. Narrowing matters on a busy host: the ring fills in seconds, and
/// the request under investigation is then already gone.
/// </param>
public sealed record StartRecordingApiRequest(int? WindowSeconds, string? PluginId);
