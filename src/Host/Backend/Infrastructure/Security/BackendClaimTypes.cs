namespace Callora.Host.Backend.Infrastructure.Security;

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
}
