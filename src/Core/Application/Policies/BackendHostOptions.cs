namespace Callora.Core.Application.Policies;

public sealed class BackendHostOptions
{
    public bool EnableTenantManagementApi { get; set; }

    public string DefaultTenantKey { get; set; } = "default";

    public string DefaultTenantDisplayName { get; set; } = "Default Tenant";

    public string AuditFilePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "plugins", "audit-log.jsonl");

    public string AdminShellBaseUrl { get; set; } = "/admin/";

    public string WorkspaceShellBaseUrl { get; set; } = "/";

    public string PluginAssetBaseUrl { get; set; } = "/plugin-assets";

    public string PluginManifestUrl { get; set; } = "/manifests/plugin-ui-assets.manifest.json";

    /// <summary>Login attempts allowed per client and minute; 0 disables limiting.</summary>
    public int RateLimitAuthPerMinute { get; set; } = 5;

    /// <summary>General API requests allowed per client and minute; 0 disables limiting.</summary>
    public int RateLimitApiPerMinute { get; set; } = 600;

    /// <summary>Root directory for stored media assets.</summary>
    public string MediaStoragePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "media");

    /// <summary>
    /// Permits webhook targets on private/loopback addresses — development
    /// only; production keeps the SSRF egress guard active.
    /// </summary>
    public bool AllowPrivateWebhookTargets { get; set; }

    /// <summary>
    /// Key-ring directory for ASP.NET DataProtection; must live on durable
    /// storage or every restart loses access to encrypted secrets.
    /// </summary>
    public string DataProtectionKeysPath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "dataprotection-keys");

    public string DatabaseConnectionString { get; set; } =
        "Host=localhost;Port=5432;Database=callora_host;Username=callora;Password=callora";

    public bool RequireAllowlistForActivation { get; set; }

    public string[] ActivationAllowlistPluginIds { get; set; } = [];

    public string[] ActivationEntitledPluginIds { get; set; } = [];

    public BackendTenantPluginEntitlementOptions[] ActivationTenantEntitlements { get; set; } = [];

    public PluginRolloutRing DefaultActivationRolloutRing { get; set; } = PluginRolloutRing.Stable;

    public BackendTenantPluginRolloutRingOptions[] ActivationTenantRolloutRings { get; set; } = [];

    public EntitlementFailureFallbackMode EntitlementFailureFallbackMode { get; set; } =
        EntitlementFailureFallbackMode.DenyActivation;

    public string JwtIssuer { get; set; } = "callora-local";

    public string JwtAudience { get; set; } = "callora-host-api";

    public string JwtSigningKey { get; set; } = BackendSecretHygiene.DefaultJwtSigningKey;

    public string? OidcAuthority { get; set; }

    public string AuthCookieName { get; set; } = "callora_admin_auth";

    public bool AuthCookieRequireHttps { get; set; }

    public bool EnableBootstrapApiKeys { get; set; } = true;

    public bool RequireApiKeyAuthentication { get; set; } = true;

    public string ApiKeyHeaderName { get; set; } = "X-Callora-Api-Key";

    public string[] ApiKeys { get; set; } = [];

    public BackendRbacRoleOptions[] RbacRoles { get; set; } = [];

    public BackendRbacUserAssignmentOptions[] RbacUserAssignments { get; set; } = [];

    /// <summary>
    /// RBAC roles whose members may sign in through the platform-operator
    /// login (/api/auth/login). Workspace members without one of these
    /// roles must use the workspace login.
    /// <para>
    /// Being an operator grants platform <em>scope</em> (reach across
    /// workspaces), not blanket authority. Only the super-admin role bypasses
    /// permission checks; any other operator role draws its concrete rights from
    /// its <see cref="RbacRoles"/> definition. An operator role listed here but
    /// missing from <see cref="RbacRoles"/> therefore reaches every workspace
    /// yet is denied every permission-gated action (403) — such roles must be
    /// granted permissions in <see cref="RbacRoles"/>.
    /// </para>
    /// </summary>
    public string[] PlatformOperatorRoles { get; set; } = ["superadmin"];

    /// <summary>
    /// Additional origins (<c>scheme://host[:port]</c>) accepted as the source of
    /// cookie-authenticated, state-changing requests, on top of same-origin. Set
    /// this only when the admin shell is served from a different origin than the
    /// API; empty by default (same-origin only). Requests authenticated by
    /// header (Bearer/API key) are never subject to this check.
    /// </summary>
    public string[] AllowedCsrfOrigins { get; set; } = [];

    /// <summary>
    /// Base URI for RFC 9457 problem types. Defaults to a URN so no
    /// registered domain is required; point it at a documentation host
    /// (ending with "/") once one exists.
    /// </summary>
    public string ProblemTypeBaseUri { get; set; } = "urn:callora:problem:";

    /// <summary>
    /// Entitlement verdict when no explicit plugin_entitlements row exists.
    /// True suits self-hosted installs (every installed plugin usable);
    /// cloud/marketplace deployments set false so grants are explicit
    /// (PLAT-253).
    /// </summary>
    public bool DefaultPluginEntitlement { get; set; } = true;

    public string[] TrustedSignerThumbprints { get; set; } = [];

    public BackendTrustedSignerOptions[] TrustedSigners { get; set; } = [];

    public bool AllowUnsignedPlugins { get; set; }

    /// <summary>
    /// Signer key fingerprints (SHA-256 of the SPKI) that are revoked: a plugin
    /// signed by one is rejected even if the signer is otherwise trusted. Enforced
    /// at install and — via runtime rehydration re-verification — at load.
    /// </summary>
    public string[] RevokedSignerFingerprints { get; set; } = [];

    /// <summary>
    /// Revoked plugin assembly content hashes (SHA-256, hex). A plugin whose assembly
    /// hashes to one is rejected regardless of signature — the way to kill a specific
    /// compromised build (including an otherwise-allowed unsigned one).
    /// </summary>
    public string[] RevokedContentHashes { get; set; } = [];

    public BackendDemoAdminUserOptions DemoAdminUser { get; set; } = new();

    /// <summary>
    /// One-time bootstrap operator for a fresh deployment (seeded only when no
    /// users exist yet). See <see cref="BackendInitialOperatorOptions"/>.
    /// </summary>
    public BackendInitialOperatorOptions InitialOperator { get; set; } = new();

    /// <summary>
    /// Central feature flags (PLAT-263): a name→enabled map for gating risky
    /// features and cloud rollouts, queried via <c>/api/features</c>.
    /// </summary>
    public Dictionary<string, bool> FeatureFlags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
