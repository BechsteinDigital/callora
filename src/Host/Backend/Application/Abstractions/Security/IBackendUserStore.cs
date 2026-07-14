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
