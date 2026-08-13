using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Tenants;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using WorkspaceEntity = Callora.Core.Domain.Workspaces.Workspace;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// Die andere Hälfte der Zusage: Die Flächentabelle wird einmal geladen und danach wiederverwendet,
/// bis jemand schreibt.
/// <para>
/// Gezählt wird über den <see cref="DbContext"/>-Verbrauch statt über Zeit: Wie schnell etwas ist,
/// hängt an der Maschine, wie oft es die Datenbank fragt, nicht.
/// </para>
/// </summary>
public sealed class TheRouteTableIsLoadedOnceTests
{
    [Fact]
    public async Task ASecondLoadDoesNotTouchTheDatabaseAgain()
    {
        await using var provider = BuildProvider(out var databaseName);
        await SeedAsync(provider, databaseName);
        var table = provider.GetRequiredService<ISurfaceRouteTable>();

        var first = await table.LoadAsync();
        await DeleteEverythingAsync(databaseName);
        var second = await table.LoadAsync();

        // Die Datenbank ist zwischen den beiden Aufrufen leer. Käme die zweite Antwort von dort,
        // wäre sie es auch — sie kommt aus dem Cache.
        Assert.Single(first);
        Assert.Single(second);
    }

    [Fact]
    public async Task AfterInvalidationTheTableIsReadAgain()
    {
        await using var provider = BuildProvider(out var databaseName);
        await SeedAsync(provider, databaseName);
        var table = provider.GetRequiredService<ISurfaceRouteTable>();

        _ = await table.LoadAsync();
        await DeleteEverythingAsync(databaseName);
        table.Invalidate();

        var afterInvalidation = await table.LoadAsync();

        Assert.Empty(afterInvalidation);
    }

    private static ServiceProvider BuildProvider(out string databaseName)
    {
        var name = $"route-table-cache-{Guid.NewGuid()}";
        databaseName = name;

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddDbContext<HostPersistenceDbContext>(options => options.UseInMemoryDatabase(name));
        services.AddSingleton<ISurfaceRouteTable, CachedSurfaceRouteTable>();
        return services.BuildServiceProvider();
    }

    private static async Task SeedAsync(IServiceProvider provider, string databaseName)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HostPersistenceDbContext>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantKey = "tenant-a",
            DisplayName = "Tenant A",
            IsActive = true
        };
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
        context.Tenants.Add(tenant);
        context.Workspaces.Add(workspace);
        context.WorkspaceSurfaces.Add(new WorkspaceSurface
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            SurfaceKey = "default",
            DisplayName = "Default",
            SurfaceType = "spa",
            Authentication = SurfaceAuthentication.Public,
            PublicPathPrefix = "/",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        Assert.NotEqual(string.Empty, databaseName);
    }

    /// <summary>
    /// Leert die Datenbank an der Tabelle vorbei — so lässt sich unterscheiden, ob eine Antwort
    /// aus dem Cache kommt oder frisch gelesen wurde.
    /// </summary>
    private static async Task DeleteEverythingAsync(string databaseName)
    {
        await using var context = new HostPersistenceDbContext(
            new DbContextOptionsBuilder<HostPersistenceDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options);

        context.WorkspaceSurfaces.RemoveRange(context.WorkspaceSurfaces);
        await context.SaveChangesAsync();
    }
}
