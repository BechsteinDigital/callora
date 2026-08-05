using Callora.Core.Domain.Security;

namespace Callora.Core.Application.Security;

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

    /// <summary>
    /// The caller's role inside a workspace (<see cref="Domain.Workspaces.WorkspaceMembership.Role"/>),
    /// or null when the user is not a member. Drives the permission set granted
    /// on workspace login.
    /// </summary>
    Task<string?> GetWorkspaceRoleAsync(
        string externalId,
        string workspaceKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackendUser>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Users that are members of the given workspace (audit finding H1 scoping).</summary>
    Task<IReadOnlyList<BackendUser>> ListByWorkspaceAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    Task<BackendUser?> GetByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a local account. A supplied password must satisfy
    /// <see cref="BackendPasswordPolicy"/> and rotates the account's security stamp,
    /// so every session issued before the change is revoked (#104, #105).
    /// </summary>
    Task<BackendUser> UpsertCredentialsAsync(
        string externalId,
        string? email,
        string? displayName,
        string? password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables an account without deleting it (#104). Disabling rotates
    /// the security stamp, so live sessions stop working immediately. Returns false
    /// when the account does not exist.
    /// </summary>
    Task<bool> SetEnabledAsync(
        string externalId,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the account's security stamp, revoking every outstanding session
    /// (#105). Used by authorization changes that do not touch credentials.
    /// Returns false when the account does not exist.
    /// </summary>
    Task<bool> RevokeSessionsAsync(
        string externalId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(
        string externalId,
        CancellationToken cancellationToken = default);
}
