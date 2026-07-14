using Callora.Host.Backend.Application.Security;
using Callora.Host.Backend.Domain.Media;
using Callora.Host.Backend.Domain.Notifications;
using Callora.Host.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Host.Backend.Tests.Integration;

/// <summary>
/// Proves the persistence-level workspace query filter (PLAT-267) against a
/// real Postgres: a workspace-scoped context reads only its own rows, an
/// unscoped (operator/non-request) context reads all. Requires Docker;
/// skipped automatically when unavailable.
/// </summary>
[Trait("Category", "Slow")]
public sealed class WorkspaceQueryFilterIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

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
    public async Task WorkspaceScopedContext_ReadsOnlyOwnWorkspaceRows()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
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
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
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
