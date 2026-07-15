using Callora.Core.Domain.Media;
using Callora.Core.Domain.Notifications;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// The write-backstop rejects a workspace-scoped caller writing rows for any
/// other workspace — even if a store copies the WorkspaceKey from client input
/// (PLAT-267). Enforcement runs before the database round-trip, so the reject
/// cases need no container.
/// </summary>
public sealed class WorkspaceWriteBackstopTests
{
    private static DbContextOptions<HostPersistenceDbContext> UnusedDbOptions() =>
        new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused")
            .Options;

    [Fact]
    public void ScopedWrite_ToForeignWorkspace_Throws()
    {
        using var context = new HostPersistenceDbContext(UnusedDbOptions(), new StubWorkspaceScope("workspace-a"));
        context.Set<MediaItem>().Add(new MediaItem
        {
            Id = Guid.NewGuid(),
            WorkspaceKey = "workspace-b",
            FileName = "b.mp3",
            ContentType = "audio/mpeg",
            SizeBytes = 1,
            Folder = "announcements",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public void ScopedWrite_ToGlobalRow_Throws()
    {
        using var context = new HostPersistenceDbContext(UnusedDbOptions(), new StubWorkspaceScope("workspace-a"));
        context.Set<NotificationEntry>().Add(new NotificationEntry
        {
            Id = Guid.NewGuid(),
            WorkspaceKey = null,
            Title = "x",
            Message = "y",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }
}
