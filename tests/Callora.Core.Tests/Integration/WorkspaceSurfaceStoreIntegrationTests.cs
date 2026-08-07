using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Tenants;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;
using WorkspaceEntity = Callora.Core.Domain.Workspaces.Workspace;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Proves the workspace-surface store and the migration backfill against a real
/// Postgres (ADR-014 §5). Requires Docker; skipped automatically when unavailable.
/// </summary>
[Trait("Category", "Slow")]
public sealed class WorkspaceSurfaceStoreIntegrationTests : IAsyncLifetime
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
    public async Task Store_UpsertGetListDelete_RoundTrips()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = Options();

        await using var context = new HostPersistenceDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedWorkspaceAsync(context, "workspace-a");

        var store = new EfWorkspaceSurfaceStore(context);

        var created = await store.UpsertAsync("workspace-a", new WorkspaceSurfaceInput(
            SurfaceKey: "portal",
            DisplayName: "Customer Portal",
            SurfaceType: "spa",
            PublicBaseUrl: "portal.example.de",
            PublicHost: "portal.example.de",
            PublicPathPrefix: "/",
            AccessMode: SurfaceAccessMode.Authenticated,
            Locale: "de",
            TemplatePluginId: null,
            TemplateVersion: null,
            ThemePluginId: "customer.theme",
            ThemeVersion: "1.0.0",
            IsActive: true));

        Assert.NotNull(created);
        Assert.Equal("portal", created!.SurfaceKey);
        Assert.Equal(SurfaceAccessMode.Authenticated, created.AccessMode);
        Assert.Equal("customer.theme", created.ThemePluginId);

        var fetched = await store.GetAsync("workspace-a", "portal");
        Assert.NotNull(fetched);
        Assert.Equal("Customer Portal", fetched!.DisplayName);

        // Upsert again updates in place (same key), does not create a second row.
        var updated = await store.UpsertAsync("workspace-a", new WorkspaceSurfaceInput(
            "portal", "Renamed", "spa", null, null, "/", SurfaceAccessMode.Public,
            null, null, null, null, null, true));
        Assert.Equal("Renamed", updated!.DisplayName);
        Assert.Single(await store.ListAsync("workspace-a"));

        Assert.Equal(SurfaceDeleteResult.Deleted, await store.DeleteAsync("workspace-a", "portal"));
        Assert.Empty(await store.ListAsync("workspace-a"));
    }

    [SkippableFact]
    public async Task IdentityAssignment_RoundTripsAndSurvivesASurfaceEdit()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = Options();

        await using var context = new HostPersistenceDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedWorkspaceAsync(context, "workspace-identity");

        var store = new EfWorkspaceSurfaceStore(context);
        await store.UpsertAsync("workspace-identity", new WorkspaceSurfaceInput(
            "portal", "Portal", "spa", null, null, "/", SurfaceAccessMode.Authenticated,
            null, null, null, null, null, true));

        var assigned = await store.AssignIdentityProviderAsync(
            "workspace-identity", "portal", "crm", "1.2.0", "operator@example.de");

        Assert.Equal("crm", assigned!.IdentityPluginId);
        Assert.Equal("1.2.0", assigned.IdentityVersion);
        Assert.Equal("operator@example.de", assigned.IdentityAssignedBy);
        Assert.NotNull(assigned.IdentityAssignedAtUtc);

        // A surface edit carries no identity fields, so it must not clear who vouches
        // for the surface's visitors as a side effect.
        var renamed = await store.UpsertAsync("workspace-identity", new WorkspaceSurfaceInput(
            "portal", "Renamed", "spa", null, null, "/", SurfaceAccessMode.Authenticated,
            null, null, null, null, null, true));

        Assert.Equal("Renamed", renamed!.DisplayName);
        Assert.Equal("crm", renamed.IdentityPluginId);
        Assert.Equal("operator@example.de", renamed.IdentityAssignedBy);

        var reread = await store.GetAsync("workspace-identity", "portal");
        Assert.Equal("crm", reread!.IdentityPluginId);
    }

    [SkippableFact]
    public async Task ClearingTheIdentityProvider_DropsProvenanceButKeepsTheBoundary()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = Options();

        await using var context = new HostPersistenceDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedWorkspaceAsync(context, "workspace-clear");

        var store = new EfWorkspaceSurfaceStore(context);
        await store.UpsertAsync("workspace-clear", new WorkspaceSurfaceInput(
            "portal", "Portal", "spa", null, null, "/", SurfaceAccessMode.Mixed,
            null, null, null, null, null, true));
        await store.AssignIdentityProviderAsync(
            "workspace-clear", "portal", "crm", "1.0.0", "operator@example.de");

        var cleared = await store.AssignIdentityProviderAsync(
            "workspace-clear", "portal", null, null, "operator@example.de");

        Assert.Null(cleared!.IdentityPluginId);
        Assert.Null(cleared.IdentityVersion);
        Assert.Null(cleared.IdentityAssignedBy);
        // The instant still moves: it is the boundary from which previously issued
        // sessions stop being trusted, and that must exist without a provider too.
        Assert.NotNull(cleared.IdentityAssignedAtUtc);
    }

    [SkippableFact]
    public async Task AssignIdentityProvider_ForUnknownSurface_ReturnsNull()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = Options();

        await using var context = new HostPersistenceDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var store = new EfWorkspaceSurfaceStore(context);

        Assert.Null(await store.AssignIdentityProviderAsync(
            "no-such-workspace", "portal", "crm", "1.0.0", "operator@example.de"));
    }

    [SkippableFact]
    public async Task Upsert_ForUnknownWorkspace_ReturnsNull()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = Options();

        await using var context = new HostPersistenceDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var store = new EfWorkspaceSurfaceStore(context);

        var result = await store.UpsertAsync("no-such-workspace", new WorkspaceSurfaceInput(
            "x", "X", "spa", null, null, "/", SurfaceAccessMode.Public, null, null, null, null, null, true));

        Assert.Null(result);
    }

    // The former BackfillSql_CreatesDefaultSurfaceMirroringWorkspace test exercised the
    // AddWorkspaceSurfaces backfill against the live schema. That backfill copied the
    // workspace's route onto its default surface — columns that no longer exist since
    // the route moved to surfaces entirely. The migration stays in history and still
    // runs correctly for old databases at its own schema version; asserting it against
    // the current schema would test a state that cannot occur.

    private DbContextOptions<HostPersistenceDbContext> Options() =>
        new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

    private static async Task SeedWorkspaceAsync(
        HostPersistenceDbContext context,
        string workspaceKey,
        string? themePluginId = null)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantKey = "tenant-" + workspaceKey,
            DisplayName = "Tenant",
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
        context.Set<Tenant>().Add(tenant);
        context.Workspaces.Add(new WorkspaceEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            WorkspaceKey = workspaceKey,
            DisplayName = "Workspace",
            WorkspaceType = "team",
            IsActive = true,
            ThemePluginId = themePluginId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        });
        await context.SaveChangesAsync();
    }
}
