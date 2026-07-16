using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

[CalloraInternal("RBAC enforcement store — not a plugin contract (REV2 §7.2)")]
public interface IBackendRbacStore
{
    Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetRolePermissionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>?> GetRolePermissionsAsync(
        string role,
        CancellationToken cancellationToken = default);

    Task UpsertRoleAsync(
        string role,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveRoleAsync(
        string role,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetUserRolesAsync(
        CancellationToken cancellationToken = default);

    Task<string?> GetUserRoleAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task UpsertUserRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveUserRoleAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
