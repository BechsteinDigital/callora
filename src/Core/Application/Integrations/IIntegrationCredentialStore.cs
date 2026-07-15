using Callora.Core.Domain.Integrations;

namespace Callora.Core.Application.Integrations;

/// <summary>
/// Persistence for named machine-to-machine integration credentials (PLAT-264).
/// </summary>
public interface IIntegrationCredentialStore
{
    /// <summary>Resolves an active (non-revoked) integration by its key lookup hash.</summary>
    Task<IntegrationCredential?> FindActiveByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationCredential>> ListAsync(CancellationToken cancellationToken = default);

    Task<IntegrationCredential?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    Task AddAsync(IntegrationCredential credential, CancellationToken cancellationToken = default);

    /// <summary>Marks an integration revoked; returns false when it does not exist or is already revoked.</summary>
    Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default);
}
