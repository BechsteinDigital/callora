namespace Callora.Core.Application.Security;

/// <summary>
/// Values of the <see cref="BackendClaimTypes.CalloraScope"/> claim.
/// </summary>
public static class BackendAuthScopes
{
    /// <summary>
    /// Platform-operator session issued by the operator login or the
    /// bootstrap API key; grants access across all workspaces.
    /// </summary>
    public const string Platform = "platform";

    /// <summary>
    /// Workspace session issued by the workspace login; locked to the
    /// workspace named in <see cref="BackendClaimTypes.WorkspaceKey"/>.
    /// </summary>
    public const string Workspace = "workspace";

    /// <summary>
    /// Tenant session issued to a member of a <see cref="Callora.Core.Domain.Tenants.TenantMembership"/>;
    /// locked to the tenant named in <see cref="BackendClaimTypes.TenantKey"/>.
    /// <para>
    /// The level ADR-014 §18 calls the TenantAdmin: administers a customer's workspaces, plugin
    /// entitlement and memberships without being an operator of the instance. It is deliberately
    /// <em>not</em> a substitute for a workspace session — it never carries a workspace binding, so
    /// <see cref="WorkspaceScopeEvaluator.HasWorkspaceAccess"/> refuses it for workspace work. Whoever
    /// wants to work inside a workspace signs in to that workspace.
    /// </para>
    /// </summary>
    public const string Tenant = "tenant";
}
