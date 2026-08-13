using Callora.Core.Application.Extensions;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Tenants;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WorkspaceEntity = Callora.Core.Domain.Workspaces.Workspace;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// Das aufgelöste Theme ist teuer — sechs Datenbankzugriffe je Aufruf — und wird deshalb gehalten.
/// Diese Tests prüfen die Gegenseite: dass jeder Schreibvorgang es verwirft.
/// <para>
/// Ein vergessener Aufruf äußert sich nicht als Fehler. Er äußert sich als Betreiber, der eine
/// Farbe ändert, die Seite neu lädt und alles beim Alten sieht — bis die Rückfallzeit abläuft und
/// niemand mehr sagen kann, woran es lag.
/// </para>
/// <para>
/// Die beiden Wege über <c>ExecuteDelete</c> in einer Transaktion —
/// <c>ReplaceDefinitionsForPluginAsync</c> und <c>ClearPluginDefinitionsAsync</c> — stehen nicht
/// hier, weil der In-Memory-Provider beides nicht kann. Sie sind deshalb nicht ungeprüft: Sie
/// laufen in <c>ThemeDefinitionWritesDropTheResolvedThemeTests</c> gegen echtes Postgres.
/// </para>
/// </summary>
public sealed class TheResolvedThemeIsDroppedOnEveryWriteTests
{
    [Fact]
    public async Task ChangingAThemeValue_DropsTheResolvedTheme()
    {
        await using var context = CreateContext();
        var themeCache = new CountingThemeResolutionCache();
        var store = new EfWorkspaceThemeSettingsStore(context, themeCache);

        await store.ReplaceValuesAsync(
            "acme",
            surfaceKey: null,
            "theme-plugin",
            new Dictionary<string, string?> { ["primary-color"] = "\"#ff0000\"" });

        Assert.True(themeCache.InvalidationCount > 0);
    }

    /// <summary>
    /// Die Theme-Zuweisung am Workspace verwirft BEIDE Caches: Die Flächentabelle trägt das Theme
    /// für die Vererbung, die Theme-Auflösung dessen Werte.
    /// </summary>
    [Fact]
    public async Task AssigningAThemeToTheWorkspace_DropsBothCaches()
    {
        await using var context = CreateContext();
        var routeTable = new PassThroughSurfaceRouteTable(context);
        var themeCache = new CountingThemeResolutionCache();
        var store = new EfWorkspaceManagementStore(context, routeTable, themeCache);
        await SeedWorkspaceAsync(context);

        await store.UpsertThemeAssignmentAsync("acme", "theme-plugin", "1.0.0", "tester");

        Assert.True(themeCache.InvalidationCount > 0, "Die Theme-Werte hängen an der Zuweisung.");
        Assert.True(routeTable.InvalidationCount > 0, "Die Flächentabelle trägt das Theme für die Vererbung.");
    }

    private static async Task SeedWorkspaceAsync(HostPersistenceDbContext context)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantKey = "tenant-a",
            DisplayName = "Tenant A",
            IsActive = true
        };
        context.Tenants.Add(tenant);
        context.Workspaces.Add(new WorkspaceEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            WorkspaceKey = "acme",
            DisplayName = "Acme",
            WorkspaceType = "spa",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static HostPersistenceDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseInMemoryDatabase($"theme-cache-{Guid.NewGuid()}")
            // ReplaceDefinitionsForPluginAsync klammert seine beiden Löschvorgänge in eine
            // Transaktion — richtig so, und der In-Memory-Provider kann es nicht. Geprüft wird
            // hier die Invalidierung, nicht die Klammer; dass sie hält, sichert
            // ThemeSettingsStore gegen Postgres ab. Die Warnung als Fehler stehen zu lassen
            // hieße, diesen Test an einer Eigenschaft des Fakes scheitern zu lassen.
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);
}
