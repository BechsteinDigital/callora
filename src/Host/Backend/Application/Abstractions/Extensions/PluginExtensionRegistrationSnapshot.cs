using Callora.Host.Backend.Application.Abstractions.Plugins;

namespace Callora.Host.Backend.Application.Abstractions.Extensions;

/// <summary>
/// Captures extension registrations and capabilities of one installed plugin.
/// </summary>
public sealed record PluginExtensionRegistrationSnapshot(
    string PluginId,
    IReadOnlyList<PluginPackageExtensionRegistration> ExtensionRegistrations,
    IReadOnlyList<string> Capabilities);
