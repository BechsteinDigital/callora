namespace Callora.Host.Backend.Domain.Extensions;

/// <summary>
/// Represents the current extension point registry state.
/// </summary>
public sealed record ExtensionPointRegistrySnapshot(
    string RegistryVersion,
    IReadOnlyList<ExtensionPointDefinition> ExtensionPoints);
