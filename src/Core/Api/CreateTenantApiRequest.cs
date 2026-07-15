namespace Callora.Core.Api;

public sealed record CreateTenantApiRequest(
    string TenantKey,
    string DisplayName);
