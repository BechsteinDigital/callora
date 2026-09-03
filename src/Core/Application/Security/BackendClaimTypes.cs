namespace Callora.Core.Application.Security;

/// <summary>
/// Claim types used by backend authorization policies.
/// </summary>
public static class BackendClaimTypes
{
    /// <summary>
    /// Permission claim type following &lt;function&gt;.&lt;action&gt; keys.
    /// </summary>
    public const string Permission = "permission";

    /// <summary>
    /// Scope claim type used by OAuth-style tokens.
    /// </summary>
    public const string Scope = "scope";

    /// <summary>
    /// Workspace binding claim stamped by workspace logins; principals
    /// carrying it are locked to that workspace.
    /// </summary>
    public const string WorkspaceKey = "workspace_key";

    /// <summary>
    /// The tenant a <see cref="BackendAuthScopes.Tenant"/> session is bound to.
    /// </summary>
    public const string TenantKey = "tenant_key";

    /// <summary>
    /// Authorization tier of the session (see <see cref="BackendAuthScopes"/>).
    /// Stamped at token issuance; a principal without it never gains
    /// platform-wide access.
    /// </summary>
    public const string CalloraScope = "callora_scope";

    /// <summary>
    /// The account's security stamp at the moment the session was issued
    /// (<see cref="BackendSecurityStamp"/>). A request whose stamp no longer
    /// matches the stored one is rejected — that is how a password change,
    /// deactivation or RBAC change revokes live sessions (#105).
    /// </summary>
    public const string SecurityStamp = "sst";

    /// <summary>
    /// Unique identifier of this session (JWT <c>jti</c>), so a single session can
    /// be revoked on logout without touching the account's other sessions.
    /// </summary>
    public const string TokenId = "jti";
}
