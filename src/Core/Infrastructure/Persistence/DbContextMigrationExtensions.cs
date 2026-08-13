using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public static class DbContextMigrationExtensions
{
    /// <summary>
    /// How long a single migration statement may take. Startup work, not request work.
    /// </summary>
    /// <remarks>
    /// The request path runs on a much shorter command timeout (see
    /// <see cref="BackendPersistenceServiceCollectionExtensions"/>), and a migration would trip over
    /// it: an index over a grown table takes minutes, not seconds. Sharing one value means picking
    /// between a request that hangs and a migration that dies halfway — which is why there are two.
    /// </remarks>
    public static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Applies pending EF migrations. Failures propagate loudly: the previous
    /// silent EnsureCreated fallback produced schema drift between database
    /// and migration history and is intentionally removed.
    /// </summary>
    public static Task ApplyMigrationsAsync(
        this HostPersistenceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        dbContext.Database.SetCommandTimeout(MigrationCommandTimeout);
        return dbContext.Database.MigrateAsync(cancellationToken);
    }
}
