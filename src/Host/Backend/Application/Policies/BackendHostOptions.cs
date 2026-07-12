namespace Callora.Host.Backend.Application.Policies;

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

    public string JwtSigningKey { get; set; } = "callora-local-dev-signing-key-change-me";

    public string? OidcAuthority { get; set; }

    public string AuthCookieName { get; set; } = "callora_admin_auth";

    public bool AuthCookieRequireHttps { get; set; }

    public bool EnableBootstrapApiKeys { get; set; } = true;

    public bool RequireApiKeyAuthentication { get; set; } = true;

    public string ApiKeyHeaderName { get; set; } = "X-Callora-Api-Key";

    public string[] ApiKeys { get; set; } = [];

    public BackendRbacRoleOptions[] RbacRoles { get; set; } = [];

    public BackendRbacUserAssignmentOptions[] RbacUserAssignments { get; set; } = [];

    public string[] TrustedSignerThumbprints { get; set; } = [];

    public BackendTrustedSignerOptions[] TrustedSigners { get; set; } = [];

    public bool AllowUnsignedPlugins { get; set; }

    public BackendDemoAdminUserOptions DemoAdminUser { get; set; } = new();
}
