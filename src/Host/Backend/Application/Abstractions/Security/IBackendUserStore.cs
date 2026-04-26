using Callora.Host.Backend.Domain.Security;

namespace Callora.Host.Backend.Application.Abstractions.Security;

public interface IBackendUserStore
{
    Task<BackendUser?> AuthenticateAsync(
        string login,
        string password,
        CancellationToken cancellationToken = default);

    Task<bool> IsWorkspaceMemberAsync(
        string externalId,
        string workspaceKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackendUser>> ListAsync(CancellationToken cancellationToken = default);

    Task<BackendUser?> GetByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default);

    Task<BackendUser> UpsertCredentialsAsync(
        string externalId,
        string? email,
        string? displayName,
        string? password,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(
        string externalId,
        CancellationToken cancellationToken = default);
}
