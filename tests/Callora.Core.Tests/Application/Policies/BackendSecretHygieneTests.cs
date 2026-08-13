using Callora.Core.Application.Policies;

namespace Callora.Core.Tests.Application.Policies;

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
        };

        // Drei statt vier, seit der re-seedende Demo-Admin entfernt ist: Sein Verstoß war der
        // vierte. Eine Zahl statt einer Aufzählung, weil der Test genau das prüfen soll — dass
        // ALLE greifen und nicht nur die, an die jemand beim Schreiben gedacht hat.
        Assert.Equal(3, BackendSecretHygiene.Inspect(options).Count);
    }

    [Fact]
    public void AllowingUnsignedPluginsIsAViolation()
    {
        // Not a secret, but the same consequence: a plugin runs as host code, so an unsigned package
        // is code of unestablished origin with the process's full rights. The trust model calls this
        // tier "production: always blocked", and this is where that stops being advice.
        var options = Secure();
        options.AllowUnsignedPlugins = true;

        var violation = Assert.Single(BackendSecretHygiene.Inspect(options));

        Assert.Contains("AllowUnsignedPlugins", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiringSignaturesTripsNothing()
    {
        Assert.Empty(BackendSecretHygiene.Inspect(Secure()));
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
    };
}
