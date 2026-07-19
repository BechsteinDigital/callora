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

        Assert.True(await store.DeleteAsync("workspace-a", "portal"));
        Assert.Empty(await store.ListAsync("workspace-a"));
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

    [SkippableFact]
    public async Task BackfillSql_CreatesDefaultSurfaceMirroringWorkspace()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");
        var options = Options();

        await using var context = new HostPersistenceDbContext(options);
        await context.Database.EnsureCreatedAsync();
        await SeedWorkspaceAsync(context, "workspace-b", host: "b.example.de", themePluginId: "b.theme");

        // Same backfill statement the AddWorkspaceSurfaces migration runs.
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO workspace_surfaces (
                "Id", workspace_id, surface_key, display_name, surface_type,
                public_base_url, public_host, public_path_prefix, access_mode, locale,
                template_plugin_id, template_version,
                theme_plugin_id, theme_version, theme_assigned_by, theme_assigned_at_utc,
                is_active, created_at_utc, updated_at_utc)
            SELECT
                gen_random_uuid(), w."Id", 'default', w."DisplayName", 'spa',
                w.public_base_url, w.public_host, w.public_path_prefix, 'Mixed', NULL,
                NULL, NULL,
                w.theme_plugin_id, w.theme_version, w.theme_assigned_by, w.theme_assigned_at_utc,
                w."IsActive", now(), now()
            FROM workspaces w;
            """);

        var surface = await new EfWorkspaceSurfaceStore(context).GetAsync("workspace-b", "default");
        Assert.NotNull(surface);
        Assert.Equal("b.example.de", surface!.PublicHost);
        Assert.Equal("b.theme", surface.ThemePluginId);
        Assert.Equal(SurfaceAccessMode.Mixed, surface.AccessMode);
    }

    private DbContextOptions<HostPersistenceDbContext> Options() =>
        new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

    private static async Task SeedWorkspaceAsync(
        HostPersistenceDbContext context,
        string workspaceKey,
        string? host = null,
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
            PublicHost = host,
            PublicPathPrefix = "/",
            ThemePluginId = themePluginId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        });
        await context.SaveChangesAsync();
    }
}
