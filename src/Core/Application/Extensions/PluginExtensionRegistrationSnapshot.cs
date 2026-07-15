using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Extensions;

/// <summary>
/// Captures extension registrations and capabilities of one installed plugin.
/// </summary>
public sealed record PluginExtensionRegistrationSnapshot(
    string PluginId,
    IReadOnlyList<PluginPackageExtensionRegistration> ExtensionRegistrations,
    IReadOnlyList<string> Capabilities);
