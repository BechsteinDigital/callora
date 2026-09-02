using Callora.Core.Domain.Security;

namespace Callora.Core.Domain.Workspaces;

public sealed class WorkspaceMembership
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public Workspace Workspace { get; set; } = null!;

    public Guid UserId { get; set; }

    public BackendUser User { get; set; } = null!;

    /// <summary>
    /// Die Mitgliedsrolle: <c>admin</c> oder alles andere. Sie entscheidet den Boden, auf dem eine
    /// Sitzung steht — was darüber hinaus gilt, steht in <see cref="Roles"/>.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Zusätzlich zugewiesene Rollen, beliebig viele.</summary>
    public ICollection<WorkspaceMembershipRole> Roles { get; set; } = new List<WorkspaceMembershipRole>();

    public DateTimeOffset AssignedAtUtc { get; set; }
}
