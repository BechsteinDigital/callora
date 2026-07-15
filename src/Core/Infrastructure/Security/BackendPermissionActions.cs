namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Allowed action keys for role-function-action RBAC mapping.
/// </summary>
public static class BackendPermissionActions
{
    public const string Create = "create";
    public const string Read = "read";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Execute = "execute";

    public static readonly string[] All =
    [
        Create,
        Read,
        Update,
        Delete,
        Execute
    ];
}
