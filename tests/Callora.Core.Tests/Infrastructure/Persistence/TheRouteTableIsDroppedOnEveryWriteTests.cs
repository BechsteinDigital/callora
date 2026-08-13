using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Tenants;
using Callora.Core.Domain.Workspaces;
using WorkspaceEntity = Callora.Core.Domain.Workspaces.Workspace;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// Der Routen-Cache hält nicht nur Daten fest, sondern eine Sicherheitsentscheidung: Eine
/// abgeschaltete Elternfläche nimmt ihre Kinder mit vom Netz (ADR-019), und ein Eintrag, der das
/// überlebt, liefert abgeschaltete Seiten weiter aus.
/// <para>
/// Deshalb prüfen diese Tests nicht, dass gecacht wird — sie prüfen, dass jeder Schreibvorgang den
/// Cache wegwirft. Ein vergessener Aufruf fällt sonst nirgends auf: Die Anwendung antwortet
/// weiterhin, nur mit dem Stand von vorhin, und die Ablaufzeit verwischt den Zusammenhang
/// vollends.
/// </para>
/// </summary>
public sealed class TheRouteTableIsDroppedOnEveryWriteTests
{
    [Fact]
    public async Task UpsertingAWorkspace_DropsTheTable()
    {
        await using var context = CreateContext();
        var routeTable = new PassThroughSurfaceRouteTable(context);
        var store = new EfWorkspaceManagementStore(context, routeTable, new CountingThemeResolutionCache());
        context.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            TenantKey = "tenant-a",
            DisplayName = "Tenant A",
            IsActive = true
        });
        await context.SaveChangesAsync();

        await store.UpsertAsync("tenant-a", "acme", "Acme", "spa", isActive: true);

        Assert.True(routeTable.InvalidationCount > 0, "Ein Upsert setzt Host, Pfad und Aktivierung der Default-Fläche.");
    }

    /// <summary>
    /// Der Fall, um den es wirklich geht: Eine Fläche wird abgeschaltet. Bliebe der Cache stehen,
    /// wäre sie weiter erreichbar — und ihre Kinder mit ihr.
    /// </summary>
    [Fact]
    public async Task DeactivatingASurface_DropsTheTable()
    {
        await using var context = CreateContext();
        var routeTable = new PassThroughSurfaceRouteTable(context);
        var surfaceStore = new EfWorkspaceSurfaceStore(context, routeTable, new CountingThemeResolutionCache());
        var workspaceId = await SeedWorkspaceAsync(context);

        await surfaceStore.UpsertAsync("acme", SurfaceInput(isActive: true));
        var afterCreate = routeTable.InvalidationCount;

        await surfaceStore.UpsertAsync("acme", SurfaceInput(isActive: false));

        Assert.True(
            routeTable.InvalidationCount > afterCreate,
            "Das Abschalten einer Fläche muss den Cache verwerfen — sonst bleibt sie erreichbar.");
        Assert.NotEqual(Guid.Empty, workspaceId);
    }

    [Fact]
    public async Task AssigningATheme_DropsTheTable()
    {
        await using var context = CreateContext();
        var routeTable = new PassThroughSurfaceRouteTable(context);
        var store = new EfWorkspaceManagementStore(context, routeTable, new CountingThemeResolutionCache());
        _ = await SeedWorkspaceAsync(context);
        var before = routeTable.InvalidationCount;

        await store.UpsertThemeAssignmentAsync("acme", "theme-plugin", "1.0.0", "tester");

        // Das Theme steht in der gecachten Menge: EffectiveSurface liest es aus dem Workspace,
        // wenn keine Fläche der Kette ein eigenes setzt.
        Assert.True(routeTable.InvalidationCount > before);
    }

    private static WorkspaceSurfaceInput SurfaceInput(bool isActive) =>
        new(
            SurfaceKey: "portal",
            DisplayName: "Portal",
            SurfaceType: "spa",
            PublicBaseUrl: null,
            PublicHost: null,
            PublicPathPrefix: "/portal",
            Authentication: SurfaceAuthentication.Public,
            Locale: null,
            TemplatePluginId: null,
            TemplateVersion: null,
            ThemePluginId: null,
            ThemeVersion: null,
            IsActive: isActive);

    private static async Task<Guid> SeedWorkspaceAsync(HostPersistenceDbContext context)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantKey = "tenant-a",
            DisplayName = "Tenant A",
            IsActive = true
        };
        context.Tenants.Add(tenant);

        var workspace = new WorkspaceEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            WorkspaceKey = "acme",
            DisplayName = "Acme",
            WorkspaceType = "spa",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();
        return workspace.Id;
    }

    /// <summary>
    /// In-Memory reicht: Geprüft wird, ob der Store die Tabelle verwirft, nicht wie Postgres
    /// schreibt. Das hält den Test ohne Docker lauffähig.
    /// </summary>
    private static HostPersistenceDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseInMemoryDatabase($"route-table-{Guid.NewGuid()}")
            .Options);
}
