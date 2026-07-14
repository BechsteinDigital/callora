using Callora.Host.Backend.Application.Policies;

namespace Callora.Host.Backend.Tests.Application.Policies;

public sealed class BackendSecretHygieneTests
{
    [Fact]
    public void SecureConfiguration_HasNoViolations()
    {
        Assert.Empty(BackendSecretHygiene.Inspect(Secure()));
    }

    [Fact]
    public void DefaultJwtSigningKey_WithoutOidc_IsAViolation()
    {
        var options = Secure();
        options.JwtSigningKey = BackendSecretHygiene.DefaultJwtSigningKey;

        var violations = BackendSecretHygiene.Inspect(options);

        Assert.Single(violations);
        Assert.Contains("JwtSigningKey", violations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultJwtSigningKey_WithOidcAuthority_IsSuppressed()
    {
        var options = Secure();
        options.JwtSigningKey = BackendSecretHygiene.DefaultJwtSigningKey;
        options.OidcAuthority = "https://login.example.com";

        Assert.Empty(BackendSecretHygiene.Inspect(options));
    }

    [Fact]
    public void EnabledDemoAdmin_WithDefaultPassword_IsAViolation()
    {
        var options = Secure();
        options.DemoAdminUser = new BackendDemoAdminUserOptions
        {
            Enabled = true,
            Password = BackendSecretHygiene.DefaultDemoAdminPassword
        };

        var violations = BackendSecretHygiene.Inspect(options);

        Assert.Single(violations);
        Assert.Contains("DemoAdminUser", violations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void DisabledDemoAdmin_WithDefaultPassword_IsClean()
    {
        var options = Secure();
        options.DemoAdminUser = new BackendDemoAdminUserOptions
        {
            Enabled = false,
            Password = BackendSecretHygiene.DefaultDemoAdminPassword
        };

        Assert.Empty(BackendSecretHygiene.Inspect(options));
    }

    [Fact]
    public void EnabledDemoAdmin_WithCustomPassword_IsClean()
    {
        var options = Secure();
        options.DemoAdminUser = new BackendDemoAdminUserOptions
        {
            Enabled = true,
            Password = "a-strong-operator-password"
        };

        Assert.Empty(BackendSecretHygiene.Inspect(options));
    }

    [Fact]
    public void DefaultDatabasePassword_IsAViolation()
    {
        var options = Secure();
        options.DatabaseConnectionString =
            "Host=localhost;Port=5432;Database=callora_host;Username=callora;Password=callora";

        var violations = BackendSecretHygiene.Inspect(options);

        Assert.Single(violations);
        Assert.Contains("DatabaseConnectionString", violations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void BootstrapApiKey_IsAViolation()
    {
        var options = Secure();
        options.ApiKeys = ["a-real-key", BackendSecretHygiene.DefaultApiKey];

        var violations = BackendSecretHygiene.Inspect(options);

        Assert.Single(violations);
        Assert.Contains("ApiKeys", violations[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AllDefaultsActive_ReportsEveryViolation()
    {
        var options = new BackendHostOptions
        {
            OidcAuthority = null,
            JwtSigningKey = BackendSecretHygiene.DefaultJwtSigningKey,
            DatabaseConnectionString =
                "Host=localhost;Port=5432;Database=callora_host;Username=callora;Password=callora",
            ApiKeys = [BackendSecretHygiene.DefaultApiKey],
            DemoAdminUser = new BackendDemoAdminUserOptions
            {
                Enabled = true,
                Password = BackendSecretHygiene.DefaultDemoAdminPassword
            }
        };

        Assert.Equal(4, BackendSecretHygiene.Inspect(options).Count);
    }

    /// <summary>
    /// Baseline options with every repository-known default overridden, so each
    /// test can trip exactly one violation in isolation.
    /// </summary>
    private static BackendHostOptions Secure() => new()
    {
        JwtSigningKey = "a-strong-production-signing-key-value-01",
        DatabaseConnectionString = "Host=db;Port=5432;Database=callora_host;Username=app;Password=Str0ngP@ss",
        ApiKeys = ["a-real-api-key"],
        DemoAdminUser = new BackendDemoAdminUserOptions { Enabled = false, Password = "changed-strong" }
    };
}
