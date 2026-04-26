namespace Callora.Host.Backend.Domain.Extensions;

/// <summary>
/// Represents one active extension entry visible to a tenant.
/// </summary>
public sealed record ActiveExtensionSnapshot(
    string TenantKey,
    string PluginId,
    string ExtensionPointId,
    ExtensionSurface Surface,
    string RequiredScope);
