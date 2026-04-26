namespace Callora.Host.Backend.Application.Policies;

public sealed class BackendTenantPluginEntitlementOptions
{
    public string TenantId { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;
}
