namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One code-first extension registration exposed by a plugin.
/// </summary>
/// <param name="ExtensionPointId">Target extension point identifier.</param>
/// <param name="Surface">Target surface code (for example: admin, workspace).</param>
public sealed record HostPluginExtensionRegistration(
    string ExtensionPointId,
    string Surface);
