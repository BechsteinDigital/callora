namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>Status payload returned by the Communication operator status route.</summary>
/// <param name="PluginId">The plugin identifier.</param>
/// <param name="Status">A coarse readiness indicator (for example <c>ok</c>).</param>
public sealed record CommunicationStatus(string PluginId, string Status);
