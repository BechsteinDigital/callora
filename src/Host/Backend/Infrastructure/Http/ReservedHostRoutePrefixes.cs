namespace Callora.Host.Backend.Infrastructure.Http;

/// <summary>
/// The host route namespaces that plugins must not overlay. A plugin route
/// whose template collides with one of these is rejected during routing
/// refresh so no plugin can shadow platform-critical endpoints such as
/// <c>/api/auth/login</c> (audit finding H2). This is the single place to
/// extend when a new host endpoint group is mapped in Program.cs.
/// </summary>
public static class ReservedHostRoutePrefixes
{
    private static readonly string[] Prefixes =
    [
        "/api/auth",
        "/api/config",
        "/api/custom-fields",
        "/api/entitlements",
        "/api/ext/admin",
        "/api/flows",
        "/api/jobs",
        "/api/media",
        "/api/notifications",
        "/api/plugins",
        "/api/security/integrations",
        "/api/security/rbac",
        "/api/tenants",
        "/api/themes",
        "/api/users",
        "/api/webhooks",
        "/api/workspaces",
        "/workspace/auth",
        "/workspace/themes"
    ];

    /// <summary>
    /// True when <paramref name="pathTemplate"/> equals a reserved prefix or
    /// sits below it as a route segment (so <c>/api/auth/login</c> collides
    /// with <c>/api/auth</c>, but <c>/api/authorizations</c> does not).
    /// </summary>
    public static bool Collides(string? pathTemplate)
    {
        if (string.IsNullOrWhiteSpace(pathTemplate))
        {
            return false;
        }

        var normalized = Normalize(pathTemplate);
        foreach (var prefix in Prefixes)
        {
            if (normalized == prefix ||
                normalized.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string pathTemplate)
    {
        var trimmed = pathTemplate.Trim().ToLowerInvariant();
        if (trimmed.Length > 1)
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
}
