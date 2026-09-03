using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Domain.Security;

public sealed class BackendUser
{
    public Guid Id { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public string? PasswordHashAlgorithm { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>
    /// Opaque value stamped into every issued session. Rotating it revokes all of
    /// this account's sessions at once — the mechanism behind password changes,
    /// deactivation and authorization changes taking effect immediately (#105).
    /// </summary>
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>
    /// A disabled account keeps its data, memberships and audit trail but
    /// authenticates nowhere and has its live sessions rejected. The
    /// non-destructive alternative to deletion (#104).
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>Consecutive failed authentication attempts since the last success.</summary>
    public int FailedAccessCount { get; set; }

    /// <summary>
    /// While set and in the future, authentication is refused regardless of the
    /// supplied password — the bounded protection against credential guessing.
    /// </summary>
    public DateTimeOffset? LockoutEndsAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<BackendRbacUserRole> RoleAssignments { get; set; } = new List<BackendRbacUserRole>();

    public ICollection<WorkspaceMembership> WorkspaceMemberships { get; set; } = new List<WorkspaceMembership>();

    public ICollection<Tenants.TenantMembership> TenantMemberships { get; set; } = new List<Tenants.TenantMembership>();
}
