using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public static class DbContextMigrationExtensions
{
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
        return dbContext.Database.MigrateAsync(cancellationToken);
    }
}
