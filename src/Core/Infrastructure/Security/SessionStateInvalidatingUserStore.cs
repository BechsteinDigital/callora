using Callora.Core.Application.Security;
using Callora.Core.Domain.Security;
using Callora.Core.Extensibility;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Keeps <see cref="BackendSessionStateCache"/> honest (#105): every store operation
/// that rotates an account's security stamp or removes the account drops its cached
/// state, so revocation takes effect on the next request instead of after the cache
/// window.
/// <para>
/// A decorator rather than a concern of the persistence store: the store owns
/// account data, not the request-path cache. Reads pass straight through.
/// </para>
/// </summary>
[CalloraInternal("Session-cache invalidation decorator — not a plugin contract (REV2 §7.2)")]
public sealed class SessionStateInvalidatingUserStore(
    IBackendUserStore inner,
    BackendSessionStateCache stateCache) : IBackendUserStore
{
    public Task<BackendUser?> AuthenticateAsync(
        string login,
        string password,
        CancellationToken cancellationToken = default) =>
        inner.AuthenticateAsync(login, password, cancellationToken);

    public Task<bool> IsWorkspaceMemberAsync(
        string externalId,
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        inner.IsWorkspaceMemberAsync(externalId, workspaceKey, cancellationToken);

    public Task<string?> GetWorkspaceRoleAsync(
        string externalId,
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        inner.GetWorkspaceRoleAsync(externalId, workspaceKey, cancellationToken);

    public Task<string?> GetTenantRoleAsync(
        string externalId,
        string tenantKey,
        CancellationToken cancellationToken = default) =>
        inner.GetTenantRoleAsync(externalId, tenantKey, cancellationToken);

    public Task<IReadOnlyList<BackendUser>> ListAsync(CancellationToken cancellationToken = default) =>
        inner.ListAsync(cancellationToken);

    public Task<IReadOnlyList<BackendUser>> ListByWorkspaceAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        inner.ListByWorkspaceAsync(workspaceKey, cancellationToken);

    public Task<BackendUser?> GetByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default) =>
        inner.GetByExternalIdAsync(externalId, cancellationToken);

    public async Task<BackendUser> UpsertCredentialsAsync(
        string externalId,
        string? email,
        string? displayName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var user = await inner
            .UpsertCredentialsAsync(externalId, email, displayName, password, cancellationToken)
            .ConfigureAwait(false);
        stateCache.Invalidate(user.ExternalId);
        return user;
    }

    public async Task<bool> SetEnabledAsync(
        string externalId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var changed = await inner.SetEnabledAsync(externalId, enabled, cancellationToken).ConfigureAwait(false);
        Invalidate(externalId);
        return changed;
    }

    public async Task<bool> RevokeSessionsAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        var changed = await inner.RevokeSessionsAsync(externalId, cancellationToken).ConfigureAwait(false);
        Invalidate(externalId);
        return changed;
    }

    public async Task<bool> RemoveAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var removed = await inner.RemoveAsync(externalId, cancellationToken).ConfigureAwait(false);
        Invalidate(externalId);
        return removed;
    }

    private void Invalidate(string externalId)
    {
        if (!string.IsNullOrWhiteSpace(externalId))
        {
            stateCache.Invalidate(externalId.Trim());
        }
    }
}
