namespace Callora.Core.Application.Security;

/// <summary>
/// A backend RBAC operation was rejected: an attempt to modify a fixed (system) role, a
/// reference to an undefined role, or a permission that is not valid for a role. These are
/// caller-facing faults with stable codes, shared by every <c>IBackendRbacStore</c>
/// implementation so the rejection is identical across storage backends.
/// </summary>
public sealed class BackendRbacException : CalloraException
{
    private const int Conflict = 409;
    private const int NotFound = 404;
    private const int BadRequest = 400;

    private BackendRbacException(string errorCode, int statusCode, string message)
        : base(errorCode, statusCode, message)
    {
    }

    /// <summary>Error code for an attempt to modify a fixed (system) role.</summary>
    public const string RoleFixedCode = "RBAC__ROLE_FIXED";

    /// <summary>Error code for a reference to an undefined role.</summary>
    public const string RoleNotFoundCode = "RBAC__ROLE_NOT_FOUND";

    /// <summary>Error code for a permission that is not valid for the role.</summary>
    public const string PermissionInvalidCode = "RBAC__PERMISSION_INVALID";

    /// <summary>The role is fixed (system-defined) and cannot be modified.</summary>
    /// <param name="roleName">The fixed role.</param>
    public static BackendRbacException RoleFixed(string roleName) =>
        new(RoleFixedCode, Conflict, $"Role '{roleName}' is fixed and cannot be modified.");

    /// <summary>The referenced role is not defined.</summary>
    /// <param name="role">The undefined role.</param>
    public static BackendRbacException RoleNotFound(string role) =>
        new(RoleNotFoundCode, NotFound, $"Role '{role}' is not defined.");

    /// <summary>The permission is not valid for the role.</summary>
    /// <param name="permission">The rejected permission.</param>
    /// <param name="role">The role it was invalid for.</param>
    public static BackendRbacException PermissionInvalid(string permission, string role) =>
        new(PermissionInvalidCode, BadRequest, $"Permission '{permission}' is invalid for role '{role}'.");
}
