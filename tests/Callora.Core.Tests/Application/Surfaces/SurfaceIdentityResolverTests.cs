using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Application.Surfaces;

/// <summary>
/// Resolution order is the security-relevant part (ADR-017 §5.3, §6.2): an assigned
/// plugin provider always wins, and a binding the host cannot honour closes the
/// surface instead of quietly degrading to the host principal or to anonymous.
/// </summary>
public sealed class SurfaceIdentityResolverTests
{
    private const string PluginId = "crm";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task WithoutABinding_TheHostPrincipalIsUsed()
    {
        var resolver = Build(hostIdentity: HostIdentity("operator-7"));

        var result = await resolver.ResolveAsync(Surface(), Request(), Credentials());

        Assert.Equal(SurfaceIdentityResolutionStatus.Authenticated, result.Status);
        Assert.Equal(SurfaceIdentityIssuers.Host, result.Caller!.Subject.Issuer);
        Assert.Equal("operator-7", result.Caller.Subject.SubjectId);
    }

    [Fact]
    public async Task WithoutABindingAndWithoutAPrincipal_TheCallerStaysAnonymous()
    {
        var resolver = Build();

        var result = await resolver.ResolveAsync(Surface(), Request(), Credentials());

        Assert.Equal(SurfaceIdentityResolutionStatus.Anonymous, result.Status);
        Assert.False(result.IsClosed);
    }

    [Fact]
    public async Task AnAssignedProviderWins_OverTheHostPrincipal()
    {
        var hostSource = new StubSurfaceHostIdentitySource(HostIdentity("operator-7"));
        var provider = StubSurfaceIdentityProvider.Returning(PluginId, PluginIdentity("lead-42"));
        var resolver = Build(hostSource: hostSource, provider: provider);

        var result = await resolver.ResolveAsync(Surface(PluginId), Request(), Credentials());

        Assert.Equal("crm.example", result.Caller!.Subject.Issuer);
        Assert.Equal("lead-42", result.Caller.Subject.SubjectId);
        Assert.False(hostSource.WasCalled);
    }

    [Fact]
    public async Task AnUnavailablePlugin_ClosesTheSurfaceInsteadOfFallingBack()
    {
        var hostSource = new StubSurfaceHostIdentitySource(HostIdentity("operator-7"));
        var resolver = Build(
            hostSource: hostSource,
            provider: StubSurfaceIdentityProvider.Returning(PluginId, PluginIdentity("lead-42")),
            unavailablePluginIds: [PluginId]);

        var result = await resolver.ResolveAsync(Surface(PluginId), Request(), Credentials());

        Assert.Equal(SurfaceIdentityResolutionStatus.ProviderUnavailable, result.Status);
        Assert.True(result.IsClosed);
        Assert.False(hostSource.WasCalled);
    }

    [Fact]
    public async Task AnAssignedPluginWithoutAProvider_ClosesTheSurface()
    {
        var resolver = Build();

        var result = await resolver.ResolveAsync(Surface(PluginId), Request(), Credentials());

        Assert.Equal(SurfaceIdentityResolutionStatus.ProviderMissing, result.Status);
    }

    [Fact]
    public async Task AProviderOfAnotherPlugin_DoesNotSatisfyTheBinding()
    {
        var resolver = Build(
            provider: StubSurfaceIdentityProvider.Returning("other", PluginIdentity("lead-42")),
            providerPluginId: "other");

        var result = await resolver.ResolveAsync(Surface(PluginId), Request(), Credentials());

        Assert.Equal(SurfaceIdentityResolutionStatus.ProviderMissing, result.Status);
    }

    [Fact]
    public async Task AThrowingProvider_ClosesTheSurface()
    {
        var resolver = Build(provider: StubSurfaceIdentityProvider.Throwing(PluginId));

        var result = await resolver.ResolveAsync(Surface(PluginId), Request(), Credentials());

        Assert.Equal(SurfaceIdentityResolutionStatus.ProviderFailed, result.Status);
    }

    [Fact]
    public async Task AStallingProvider_HitsTheDeadlineAndClosesTheSurface()
    {
        var resolver = Build(
            provider: StubSurfaceIdentityProvider.Stalling(PluginId),
            options: new SurfaceIdentityOptions { ProviderTimeout = TimeSpan.FromMilliseconds(50) });

        var result = await resolver.ResolveAsync(Surface(PluginId), Request(), Credentials());

        Assert.Equal(SurfaceIdentityResolutionStatus.ProviderFailed, result.Status);
    }

    [Fact]
    public async Task AProviderReturningAnonymous_LeavesTheCallerAnonymous()
    {
        var resolver = Build(
            provider: StubSurfaceIdentityProvider.Returning(PluginId, HostSurfaceIdentityResult.Anonymous));

        var result = await resolver.ResolveAsync(Surface(PluginId), Request(), Credentials());

        Assert.Equal(SurfaceIdentityResolutionStatus.Anonymous, result.Status);
        Assert.False(result.IsClosed);
    }

    [Fact]
    public async Task AProviderClaimingTheHostNamespace_IsRefused()
    {
        var impersonating = HostSurfaceIdentityResult.Identified(
            SurfaceIdentityIssuers.Host, "operator-7", "password", Now.AddMinutes(-1), Now.AddHours(1));
        var resolver = Build(provider: StubSurfaceIdentityProvider.Returning(PluginId, impersonating));

        var result = await resolver.ResolveAsync(Surface(PluginId), Request(), Credentials());

        Assert.Equal(SurfaceIdentityResolutionStatus.ProviderFailed, result.Status);
    }

    [Fact]
    public async Task OnlyDeclaredCredentialsAreForwarded()
    {
        var provider = StubSurfaceIdentityProvider.Returning(
            PluginId,
            PluginIdentity("lead-42"),
            new SurfaceIdentityCredentialSource(SurfaceIdentityCredentialKind.Cookie, "crm_session"));
        var credentials = Credentials()
            .With(SurfaceIdentityCredentialKind.Cookie, "crm_session", "abc")
            .With(SurfaceIdentityCredentialKind.Cookie, "callora_admin", "secret")
            .With(SurfaceIdentityCredentialKind.Header, "Authorization", "Bearer secret");
        var resolver = Build(provider: provider);

        await resolver.ResolveAsync(Surface(PluginId), Request(), credentials);

        var forwarded = Assert.Single(provider.LastRequest!.Credentials);
        Assert.Equal("crm_session", forwarded.Name);
        Assert.Equal("abc", forwarded.Value);
    }

    [Fact]
    public async Task ADeclaredCredentialTheRequestLacks_StaysAbsentRatherThanEmpty()
    {
        var provider = StubSurfaceIdentityProvider.Returning(
            PluginId,
            PluginIdentity("lead-42"),
            new SurfaceIdentityCredentialSource(SurfaceIdentityCredentialKind.Header, "X-Crm-Session"));
        var resolver = Build(provider: provider);

        await resolver.ResolveAsync(Surface(PluginId), Request(), Credentials());

        Assert.Empty(provider.LastRequest!.Credentials);
        Assert.Null(provider.LastRequest.Credential(SurfaceIdentityCredentialKind.Header, "X-Crm-Session"));
    }

    private static SurfaceIdentityResolver Build(
        HostSurfaceIdentityResult? hostIdentity = null,
        StubSurfaceHostIdentitySource? hostSource = null,
        StubSurfaceIdentityProvider? provider = null,
        string? providerPluginId = null,
        string[]? unavailablePluginIds = null,
        SurfaceIdentityOptions? options = null)
    {
        var catalog = new StaticPluginExportCatalog();
        if (provider is not null)
        {
            catalog.Add(providerPluginId ?? PluginId, provider);
        }

        return new SurfaceIdentityResolver(
            new StaticPluginAvailabilityEvaluator(unavailablePluginIds ?? []),
            catalog,
            hostSource ?? new StubSurfaceHostIdentitySource(hostIdentity ?? HostSurfaceIdentityResult.Anonymous),
            options ?? new SurfaceIdentityOptions(),
            TimeProvider.System,
            NullLogger<SurfaceIdentityResolver>.Instance);
    }

    private static WorkspaceSurfaceSnapshot Surface(string? identityPluginId = null) =>
        new(
            Guid.NewGuid(),
            "workspace-a",
            "portal",
            "Portal",
            "spa",
            null,
            null,
            "/",
            SurfaceAuthentication.Public,
            SurfaceRouting.Tree,
            "de",
            null,
            null,
            null,
            null,
            true,
            Now,
            Now)
        {
            TenantKey = "tenant-a",
            IdentityPluginId = identityPluginId,
            IdentityVersion = identityPluginId is null ? null : "1.0.0",
            IdentityAssignedAtUtc = identityPluginId is null ? null : Now,
        };

    private static SurfaceRequestDescriptor Request() => new("GET", "/", "de");

    private static DictionarySurfaceCredentialReader Credentials() => new();

    private static HostSurfaceIdentityResult HostIdentity(string subjectId) =>
        HostSurfaceIdentityResult.Identified(
            SurfaceIdentityIssuers.Host, subjectId, "backend-session", Now.AddMinutes(-1), Now.AddHours(1));

    private static HostSurfaceIdentityResult PluginIdentity(string subjectId) =>
        HostSurfaceIdentityResult.Identified(
            "crm.example", subjectId, "password", Now.AddMinutes(-1), Now.AddHours(1));
}
