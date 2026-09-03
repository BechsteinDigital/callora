using Callora.Core.Application.Security;
using Callora.Core.Domain.Media;
using Callora.Core.Domain.Notifications;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Proves the persistence-level workspace query filter (PLAT-267) against a
/// real Postgres: a workspace-scoped context reads only its own rows, an
/// unscoped (operator/non-request) context reads all. Requires Docker;
/// skipped automatically when unavailable.
/// </summary>
[Trait("Category", "Slow")]
[Collection(PostgresCollection.Name)]
public sealed class WorkspaceQueryFilterIntegrationTests(PostgresFixture postgres)
{

    // Eine Datenbank je TEST, nicht je Aufruf: xUnit erzeugt die Klasse für jede
    // Testmethode neu, also ist dieses Feld pro Test frisch. Ohne das bekäme jeder
    // Kontext innerhalb eines Tests eine eigene Datenbank — was ein Test, der zwei
    // gleichzeitige Verbindungen gegeneinander laufen lässt, sofort bemerkt: Der
    // Schreiber landet in der einen, die Leser suchen in der anderen.
    private string? _database;

    private async Task<string> DatabaseAsync() =>
        _database ??= await postgres.CreateDatabaseAsync();
    [SkippableFact]
    public async Task WorkspaceScopedContext_ReadsOnlyOwnWorkspaceRows()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;

        // Seed with no scope (operator / non-request): writes to both workspaces.
        await using (var seed = new HostPersistenceDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Set<MediaItem>().AddRange(
                NewItem("workspace-a", "a1.mp3"),
                NewItem("workspace-a", "a2.mp3"),
                NewItem("workspace-b", "b1.mp3"));
            await seed.SaveChangesAsync();
        }

        // Scoped to workspace-a: the filter must hide workspace-b.
        await using (var scoped = new HostPersistenceDbContext(options, new StubScope("workspace-a")))
        {
            var items = await scoped.Set<MediaItem>().ToListAsync();
            Assert.Equal(2, items.Count);
            Assert.All(items, item => Assert.Equal("workspace-a", item.WorkspaceKey));
        }

        // Unscoped (operator): sees everything.
        await using (var unscoped = new HostPersistenceDbContext(options))
        {
            Assert.Equal(3, await unscoped.Set<MediaItem>().CountAsync());
        }
    }

    [SkippableFact]
    public async Task WorkspaceScopedContext_SeesOwnAndGlobalNotifications_NotForeign()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;

        await using (var seed = new HostPersistenceDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Set<NotificationEntry>().AddRange(
                NewNotification("workspace-a", "own"),
                NewNotification(null, "global"),
                NewNotification("workspace-b", "foreign"));
            await seed.SaveChangesAsync();
        }

        await using (var scoped = new HostPersistenceDbContext(options, new StubScope("workspace-a")))
        {
            var titles = await scoped.Set<NotificationEntry>().Select(x => x.Title).ToListAsync();
            Assert.Contains("own", titles);
            Assert.Contains("global", titles);
            Assert.DoesNotContain("foreign", titles);
        }
    }

    /// <summary>
    /// Eine Mandanten-Sitzung sieht die Plugin-Aktivierungen ihrer eigenen Workspaces — und nur die.
    /// </summary>
    /// <remarks>
    /// Der Grund, warum es diesen Filter überhaupt gibt: Eine Mandanten-Sitzung ist an keinen
    /// Workspace gebunden, wäre damit „nicht scoped" und liefe wie ein Operator am Backstop vorbei.
    /// Bei einer Agentur, die die Instanz für ihre Kunden betreibt, hieße das: jeder Kunde liest die
    /// Plugin-Landschaft aller anderen.
    /// </remarks>
    [SkippableFact]
    public async Task TenantScopedContext_ReadsOnlyItsOwnTenantsActivations()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;

        await using (var seed = new HostPersistenceDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Set<WorkspacePluginActivation>().AddRange(
                NewActivation("tenant-a", "workspace-a", "pbx"),
                NewActivation("tenant-a", "workspace-a2", "cms"),
                NewActivation("tenant-b", "workspace-b", "pbx"));
            await seed.SaveChangesAsync();
        }

        await using (var scoped = new HostPersistenceDbContext(options, new StubTenantScope("tenant-a")))
        {
            var rows = await scoped.Set<WorkspacePluginActivation>().ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal("tenant-a", row.TenantKey));
        }
    }

    /// <summary>
    /// Arbeit IM Workspace bleibt für eine Mandanten-Sitzung leer — der Filter ist eine positive
    /// Liste, kein Bypass.
    /// </summary>
    /// <remarks>
    /// Sichtbar ist nur, was auf Mandantenebene etwas bedeutet. Medien, Flows, Jobs und Webhooks
    /// gehören dem Workspace; wer darin arbeiten will, meldet sich dort an. Fiele die Entscheidung
    /// andersherum, wäre der Mandanten-Scope ein zweiter Operator mit anderem Namen.
    /// </remarks>
    [SkippableFact]
    public async Task TenantScopedContext_SeesNoWorkspaceWorkAtAll()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;

        await using (var seed = new HostPersistenceDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.Set<MediaItem>().Add(NewItem("workspace-a", "a1.mp3"));
            seed.Set<NotificationEntry>().AddRange(
                NewNotification("workspace-a", "own"),
                NewNotification(null, "global"));
            await seed.SaveChangesAsync();
        }

        await using (var scoped = new HostPersistenceDbContext(options, new StubTenantScope("tenant-a")))
        {
            Assert.Empty(await scoped.Set<MediaItem>().ToListAsync());

            // Auch die plattformweite Zeile nicht: Sie ist für Workspace-Sitzungen gedacht, nicht
            // dafür, dem Mandanten eine Teilsicht auf fremde Arbeit zu öffnen.
            Assert.Empty(await scoped.Set<NotificationEntry>().ToListAsync());
        }
    }

    /// <summary>Schreiben für einen fremden Mandanten scheitert, statt still durchzugehen.</summary>
    [SkippableFact]
    public async Task TenantScopedWrite_ToAForeignTenant_IsRefused()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;

        await using (var seed = new HostPersistenceDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
        }

        await using var scoped = new HostPersistenceDbContext(options, new StubTenantScope("tenant-a"));
        scoped.Set<WorkspacePluginActivation>().Add(NewActivation("tenant-b", "workspace-b", "pbx"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => scoped.SaveChangesAsync());
    }

    private static WorkspacePluginActivation NewActivation(
        string tenantKey, string workspaceKey, string pluginId) => new()
    {
        Id = Guid.NewGuid(),
        TenantKey = tenantKey,
        WorkspaceKey = workspaceKey,
        PluginId = pluginId,
        IsActive = true,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static NotificationEntry NewNotification(string? workspaceKey, string title) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceKey = workspaceKey,
        Title = title,
        Message = "test",
        Level = "info",
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static MediaItem NewItem(string workspaceKey, string fileName) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceKey = workspaceKey,
        FileName = fileName,
        ContentType = "audio/mpeg",
        SizeBytes = 1,
        Folder = "announcements",
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class StubScope(string workspaceKey) : IWorkspaceScopeContext
    {
        public bool IsWorkspaceScoped => true;

        public string? WorkspaceKey { get; } = workspaceKey;
    }

    // Bewusst ohne WorkspaceKey: Genau das ist der Fall, den es abzusichern gilt — an keinen
    // Workspace gebunden, und trotzdem kein Operator.
    private sealed class StubTenantScope(string tenantKey) : IWorkspaceScopeContext
    {
        public bool IsWorkspaceScoped => false;

        public string? WorkspaceKey => null;

        public bool IsTenantScoped => true;

        public string? TenantKey { get; } = tenantKey;
    }
}
