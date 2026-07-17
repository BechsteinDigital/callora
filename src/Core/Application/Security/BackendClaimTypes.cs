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
    /// Authorization tier of the session (see <see cref="BackendAuthScopes"/>).
    /// Stamped at token issuance; a principal without it never gains
    /// platform-wide access.
    /// </summary>
    public const string CalloraScope = "callora_scope";
}
