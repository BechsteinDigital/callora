namespace Callora.Host.Backend.Api;

public sealed record CreateTenantApiRequest(
    string TenantKey,
    string DisplayName);
