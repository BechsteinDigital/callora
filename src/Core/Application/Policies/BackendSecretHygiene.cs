namespace Callora.Core.Application.Policies;

/// <summary>
/// Central detector for repository-known development defaults that must never
/// reach a non-development deployment (audit finding C1). It is the single
/// source of truth for those default values, so the option defaults reference
/// the same constants and cannot drift away from the guard.
/// <para>
/// The type is pure: <see cref="Inspect"/> returns the list of active
/// violations, and the composition root decides the reaction — warn in
/// Development, refuse to start everywhere else.
/// </para>
/// </summary>
public static class BackendSecretHygiene
{
    /// <summary>Signing key shipped for local development; forges tokens if reused.</summary>
    public const string DefaultJwtSigningKey = "callora-local-dev-signing-key-change-me";

    /// <summary>
    /// Password of the seeded demo administrator. Satisfies
    /// <c>BackendPasswordPolicy</c> like every other local credential (#104) — a
    /// seed that skipped the policy would be the one weak super-admin in the system.
    /// </summary>
    public const string DefaultDemoAdminPassword = "callora-demo-admin!";

    /// <summary>Bootstrap API key shipped in the development configuration.</summary>
    public const string DefaultApiKey = "callora-local-dev-key-change-me";

    /// <summary>Password segment of the built-in development connection string.</summary>
    private const string DefaultDatabasePasswordSegment = "password=callora";

    /// <summary>
    /// Returns one human-readable message per active development default. An
    /// empty list means the configuration carries no known insecure default.
    /// </summary>
    public static IReadOnlyList<string> Inspect(BackendHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var violations = new List<string>();

        // A configured OIDC authority makes the local signing key irrelevant,
        // so the default key is only a risk when the host signs its own tokens.
        if (string.IsNullOrWhiteSpace(options.OidcAuthority) &&
            string.Equals(options.JwtSigningKey, DefaultJwtSigningKey, StringComparison.Ordinal))
        {
            violations.Add(
                "BackendHost.JwtSigningKey still carries the development default — configure a strong secret.");
        }

        if (options.DemoAdminUser.Enabled &&
            string.Equals(options.DemoAdminUser.Password, DefaultDemoAdminPassword, StringComparison.Ordinal))
        {
            violations.Add(
                "BackendHost.DemoAdminUser is enabled with its default password — disable it or set a strong password.");
        }

        if (!string.IsNullOrEmpty(options.DatabaseConnectionString) &&
            options.DatabaseConnectionString.Contains(DefaultDatabasePasswordSegment, StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(
                "BackendHost.DatabaseConnectionString still uses the development database password — configure real credentials.");
        }

        if (options.ApiKeys is { Length: > 0 } &&
            Array.Exists(options.ApiKeys, key => string.Equals(key, DefaultApiKey, StringComparison.Ordinal)))
        {
            violations.Add(
                "BackendHost.ApiKeys contains the development bootstrap key — replace it before exposing the host.");
        }

        // Not a secret, but the same class of mistake and the same consequence. A plugin runs as host
        // code: package signatures are the entire basis for trusting it (ADR-013), so accepting an
        // unsigned one outside development means running code of unestablished origin with the
        // process's full rights. The trust model calls this tier "production: always blocked"; until
        // now that was advice, and the development compose file sets the flag.
        if (options.AllowUnsignedPlugins)
        {
            violations.Add(
                "BackendHost.AllowUnsignedPlugins is enabled — plugins run as host code, so an unsigned "
                + "package is code of unestablished origin. Sign the packages and configure TrustedSigners.");
        }

        return violations;
    }
}
