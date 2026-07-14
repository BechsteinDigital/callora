namespace Callora.Host.Backend.Domain.Integrations;

/// <summary>
/// A named machine-to-machine credential (PLAT-264). Unlike the global bootstrap
/// API keys, an integration carries its own identity, a single assigned RBAC role
/// (never super-admin by default) and an authorization scope, so its access is
/// bounded and every call is attributable to a name.
/// </summary>
public sealed class IntegrationCredential
{
    public Guid Id { get; set; }

    /// <summary>Human-readable unique name, e.g. "billing-sync".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Deterministic SHA-256 hash of the secret key, used for O(1) lookup.</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Leading characters of the key, kept for recognition in listings.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>RBAC role whose permissions this integration acts with.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Authorization tier: "platform" (cross-workspace) or "workspace".</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Workspace this integration is locked to; required for workspace scope.</summary>
    public string? WorkspaceKey { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Identity that created the integration, for audit.</summary>
    public string? CreatedBy { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
