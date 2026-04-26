namespace Callora.Host.Backend.Domain.Security;

public sealed class BackendRbacRoleGrant
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public BackendRbacRole Role { get; set; } = null!;

    public string PermissionKey { get; set; } = string.Empty;
}
