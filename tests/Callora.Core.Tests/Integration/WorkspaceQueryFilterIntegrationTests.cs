using Callora.Core.Application.Security;
using Callora.Core.Domain.Media;
using Callora.Core.Domain.Notifications;
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
}
