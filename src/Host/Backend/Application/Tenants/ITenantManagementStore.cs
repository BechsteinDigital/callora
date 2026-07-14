namespace Callora.Host.Backend.Application.Tenants;

public interface ITenantManagementStore
{
    Task<IReadOnlyList<TenantSnapshot>> ListAsync(CancellationToken cancellationToken = default);

    Task<TenantSnapshot?> GetAsync(
        string tenantKey,
        CancellationToken cancellationToken = default);

    Task<TenantCreateResult> CreateAsync(
        string tenantKey,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<TenantSetStateResult> SetActiveStateAsync(
        string tenantKey,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<TenantDeleteResult> RemoveAsync(
        string tenantKey,
        CancellationToken cancellationToken = default);
}
