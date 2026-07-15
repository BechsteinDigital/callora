namespace Callora.Administration.Api;

public sealed record CreateTenantApiRequest(
    string TenantKey,
    string DisplayName);
