using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Thread-safe in-memory RBAC store for role permissions and user role assignments.
/// </summary>
public sealed class InMemoryBackendRbacStore : IBackendRbacStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, IReadOnlyCollection<string>> _rolePermissions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _userRoles = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryBackendRbacStore(BackendHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _rolePermissions[BackendRoles.SuperAdmin] = ["*"];

        foreach (var (role, permissions) in BackendRbacPermissionCatalog.Build(options))
        {
            if (string.Equals(role, BackendRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _rolePermissions[role] = permissions;
        }

        foreach (var assignment in options.RbacUserAssignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.UserId) || string.IsNullOrWhiteSpace(assignment.Role))
            {
                continue;
            }

            _userRoles[assignment.UserId.Trim()] = assignment.Role.Trim();
        }
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> GetRolePermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyCollection<string>>>(
                new Dictionary<string, IReadOnlyCollection<string>>(_rolePermissions, StringComparer.OrdinalIgnoreCase));
        }
    }

    public Task<IReadOnlyCollection<string>?> GetRolePermissionsAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_rolePermissions.TryGetValue(role, out var permissions)
                ? permissions
                : null);
        }
    }

    public Task UpsertRoleAsync(
        string role,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentNullException.ThrowIfNull(permissions);

        if (string.Equals(role, BackendRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            throw BackendRbacException.RoleFixed(BackendRoles.SuperAdmin);
        }

        var normalizedPermissions = NormalizePermissions(permissions, role);
        lock (_sync)
        {
            _rolePermissions[role.Trim()] = normalizedPermissions;
        }

        return Task.CompletedTask;
    }

    public Task<bool> RemoveRoleAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        if (string.Equals(role, BackendRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        lock (_sync)
        {
            return Task.FromResult(_rolePermissions.Remove(role.Trim()));
        }
    }

    public Task<IReadOnlyDictionary<string, string>> GetUserRolesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(_userRoles, StringComparer.OrdinalIgnoreCase));
        }
    }

    public Task<string?> GetUserRoleAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        lock (_sync)
        {
            return Task.FromResult(_userRoles.GetValueOrDefault(userId.Trim()));
        }
    }

    public Task UpsertUserRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        lock (_sync)
        {
            if (!_rolePermissions.ContainsKey(role.Trim()))
            {
                throw BackendRbacException.RoleNotFound(role);
            }

            _userRoles[userId.Trim()] = role.Trim();
        }

        return Task.CompletedTask;
    }

    public Task<bool> RemoveUserRoleAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        lock (_sync)
        {
            return Task.FromResult(_userRoles.Remove(userId.Trim()));
        }
    }

    private static IReadOnlyCollection<string> NormalizePermissions(IReadOnlyCollection<string> permissions, string role)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);

        foreach (var permission in permissions)
        {
            var trimmed = permission.Trim().ToLowerInvariant();
            if (!BackendPermissionKeyValidator.IsValid(trimmed))
            {
                throw BackendRbacException.PermissionInvalid(permission, role);
            }

            normalized.Add(trimmed);
        }

        return normalized.ToArray();
    }
}
