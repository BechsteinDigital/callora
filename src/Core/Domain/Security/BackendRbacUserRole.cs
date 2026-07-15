namespace Callora.Core.Domain.Security;

public sealed class BackendRbacUserRole
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public BackendUser User { get; set; } = null!;

    public Guid RoleId { get; set; }

    public BackendRbacRole Role { get; set; } = null!;

    public DateTimeOffset AssignedAtUtc { get; set; }
}
