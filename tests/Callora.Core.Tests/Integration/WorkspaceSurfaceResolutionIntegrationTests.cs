using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Tenants;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Proves surface-based public-route resolution and the workspace upsert write-through
/// (ADR-014 §5/§14) against a real Postgres. Requires Docker; skipped when unavailable.
/// </summary>
[Trait("Category", "Slow")]
public sealed class WorkspaceSurfaceResolutionIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private bool _started;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
            _started = true;
        }
        catch (Exception)
        {
            _started = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_started)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Upsert_WritesThroughDefaultSurface_AndResolutionGoesThroughSurfaces()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await FreshContextWithTenantAsync();
        var workspaceStore = new EfWorkspaceManagementStore(context);

        var upsert = await workspaceStore.UpsertAsync(
            "tenant-a", "workspace-a", "Workspace A", "team", isActive: true, defaultSurfaceBaseUrl: "portal.example.de/app");
        Assert.Equal(WorkspaceUpsertStatus.Ok, upsert.Status);

        // Write-through created a default surface mirroring the public route.
        var defaultSurface = await new EfWorkspaceSurfaceStore(context).GetAsync("workspace-a", "default");
        Assert.NotNull(defaultSurface);
        Assert.Equal("portal.example.de", defaultSurface!.PublicHost);
        Assert.Equal("/app", defaultSurface.PublicPathPrefix);

        // Resolution now matches through the surface.
        var resolved = await workspaceStore.ResolveByPublicRouteAsync("portal.example.de", "/app");
        Assert.NotNull(resolved);
        Assert.Equal("workspace-a", resolved!.WorkspaceKey);

        // A foreign host does not resolve.
        Assert.Null(await workspaceStore.ResolveByPublicRouteAsync("nope.example.de", "/app"));
    }

    [SkippableFact]
    public async Task SecondSurface_OnSameWorkspace_RoutesToThatWorkspace()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await FreshContextWithTenantAsync();
        var workspaceStore = new EfWorkspaceManagementStore(context);
        var surfaceStore = new EfWorkspaceSurfaceStore(context);

        _ = await workspaceStore.UpsertAsync(
            "tenant-a", "workspace-a", "Workspace A", "team", isActive: true, defaultSurfaceBaseUrl: "primary.example.de");

        // A second access surface on the same workspace, different host.
        await surfaceStore.UpsertAsync("workspace-a", new WorkspaceSurfaceInput(
            SurfaceKey: "partner",
            DisplayName: "Partner Portal",
            SurfaceType: "spa",
            PublicBaseUrl: "partner.example.de",
            PublicHost: "partner.example.de",
            PublicPathPrefix: "/",
            AccessMode: SurfaceAccessMode.Authenticated,
            Locale: null,
            TemplatePluginId: null,
            TemplateVersion: null,
            ThemePluginId: null,
            ThemeVersion: null,
            IsActive: true));

        var viaDefault = await workspaceStore.ResolveByPublicRouteAsync("primary.example.de", "/");
        var viaPartner = await workspaceStore.ResolveByPublicRouteAsync("partner.example.de", "/");

        Assert.Equal("workspace-a", viaDefault?.WorkspaceKey);
        Assert.Equal("workspace-a", viaPartner?.WorkspaceKey);
    }

    [SkippableFact]
    public async Task ResolveSurface_ReturnsMatchedSurface_WithAccessModeKeyLocaleAndTenant()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await FreshContextWithTenantAsync();
        var workspaceStore = new EfWorkspaceManagementStore(context);
        var surfaceStore = new EfWorkspaceSurfaceStore(context);

        _ = await workspaceStore.UpsertAsync(
            "tenant-a", "workspace-a", "Workspace A", "team", isActive: true, defaultSurfaceBaseUrl: "primary.example.de");

        await surfaceStore.UpsertAsync("workspace-a", new WorkspaceSurfaceInput(
            SurfaceKey: "partner",
            DisplayName: "Partner Portal",
            SurfaceType: "portal",
            PublicBaseUrl: "partner.example.de",
            PublicHost: "partner.example.de",
            PublicPathPrefix: "/",
            AccessMode: SurfaceAccessMode.Authenticated,
            Locale: "en",
            TemplatePluginId: null,
            TemplateVersion: null,
            ThemePluginId: null,
            ThemeVersion: null,
            IsActive: true));

        var resolved = await workspaceStore.ResolveSurfaceByPublicRouteAsync("partner.example.de", "/");

        Assert.NotNull(resolved);
        Assert.Equal("partner", resolved!.SurfaceKey);
        Assert.Equal("portal", resolved.SurfaceType);
        Assert.Equal(SurfaceAccessMode.Authenticated, resolved.AccessMode);
        Assert.Equal("en", resolved.Locale);
        Assert.Equal("workspace-a", resolved.WorkspaceKey);
        Assert.Equal("tenant-a", resolved.TenantKey);

        // A foreign host does not resolve to any surface.
        Assert.Null(await workspaceStore.ResolveSurfaceByPublicRouteAsync("nope.example.de", "/"));
    }

    [SkippableFact]
    public async Task InactiveSurface_IsNotResolved_ByEitherResolver()
    {
        // Guards the security-relevant assumption the render endpoint now relies on:
        // it dropped its own IsActive re-check and trusts the store's match loop to filter
        // inactive surfaces. This proves that filter directly against real Postgres.
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await FreshContextWithTenantAsync();
        var workspaceStore = new EfWorkspaceManagementStore(context);
        var surfaceStore = new EfWorkspaceSurfaceStore(context);

        _ = await workspaceStore.UpsertAsync(
            "tenant-a", "workspace-a", "Workspace A", "team", isActive: true, defaultSurfaceBaseUrl: "primary.example.de");

        // An inactive surface on its own host must never be served.
        await surfaceStore.UpsertAsync("workspace-a", new WorkspaceSurfaceInput(
            SurfaceKey: "retired",
            DisplayName: "Retired Portal",
            SurfaceType: "spa",
            PublicBaseUrl: "retired.example.de",
            PublicHost: "retired.example.de",
            PublicPathPrefix: "/",
            AccessMode: SurfaceAccessMode.Public,
            Locale: null,
            TemplatePluginId: null,
            TemplateVersion: null,
            ThemePluginId: null,
            ThemeVersion: null,
            IsActive: false));

        Assert.Null(await workspaceStore.ResolveSurfaceByPublicRouteAsync("retired.example.de", "/"));
        Assert.Null(await workspaceStore.ResolveByPublicRouteAsync("retired.example.de", "/"));
    }

    [SkippableFact]
    public async Task ResolveByPublicRoute_BehaviourUnchanged_AlongsideResolveSurface()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await FreshContextWithTenantAsync();
        var workspaceStore = new EfWorkspaceManagementStore(context);

        _ = await workspaceStore.UpsertAsync(
            "tenant-a", "workspace-a", "Workspace A", "team", isActive: true, defaultSurfaceBaseUrl: "portal.example.de/app");

        // The workspace-level resolver keeps returning the owning workspace unchanged after
        // the helper extraction — the new surface resolver is purely additive.
        var workspace = await workspaceStore.ResolveByPublicRouteAsync("portal.example.de", "/app");
        var surface = await workspaceStore.ResolveSurfaceByPublicRouteAsync("portal.example.de", "/app");

        Assert.Equal("workspace-a", workspace?.WorkspaceKey);
        Assert.Equal("workspace-a", surface?.WorkspaceKey);
        Assert.Equal("default", surface?.SurfaceKey);
        Assert.Null(await workspaceStore.ResolveByPublicRouteAsync("nope.example.de", "/app"));
    }

    [SkippableFact]
    public async Task RepeatedUpsert_UpdatesDefaultSurfaceInPlace_WithoutDuplicating()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        await using var context = await FreshContextWithTenantAsync();
        var workspaceStore = new EfWorkspaceManagementStore(context);

        _ = await workspaceStore.UpsertAsync(
            "tenant-a", "workspace-a", "Workspace A", "team", isActive: true, defaultSurfaceBaseUrl: "first.example.de");
        _ = await workspaceStore.UpsertAsync(
            "tenant-a", "workspace-a", "Workspace A", "team", isActive: true, defaultSurfaceBaseUrl: "second.example.de/x");

        var defaults = (await new EfWorkspaceSurfaceStore(context).ListAsync("workspace-a"))
            .Where(s => s.SurfaceKey == "default")
            .ToList();

        Assert.Single(defaults); // create-if-missing is idempotent — no second default surface
        Assert.Equal("second.example.de", defaults[0].PublicHost); // updated in place
        Assert.Equal("/x", defaults[0].PublicPathPrefix);
    }

    private async Task<HostPersistenceDbContext> FreshContextWithTenantAsync()
    {
        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var context = new HostPersistenceDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var nowUtc = DateTimeOffset.UtcNow;
        context.Set<Tenant>().Add(new Tenant
        {
            Id = Guid.NewGuid(),
            TenantKey = "tenant-a",
            DisplayName = "Tenant A",
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        });
        await context.SaveChangesAsync();
        return context;
    }
}
