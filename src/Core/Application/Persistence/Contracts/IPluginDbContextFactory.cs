using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Application.Persistence.Contracts;

/// <summary>
/// Builds and migrates a plugin's own EF Core <see cref="DbContext"/>
/// (PLAT-260). Callora is database-backed: plugins model their data as real
/// typed entities in a dedicated Postgres schema (<c>plugin_&lt;id&gt;</c>),
/// not as JSON documents. The host resolves this factory into the plugin's
/// curated service provider, pointing EF at the shared host database with
/// the plugin assembly as its migrations assembly.
/// </summary>
/// <typeparam name="TContext">
/// The plugin's DbContext. It must expose a constructor taking
/// <see cref="DbContextOptions{TContext}"/> and set its schema via
/// <c>modelBuilder.HasDefaultSchema("plugin_&lt;id&gt;")</c>.
/// </typeparam>
public interface IPluginDbContextFactory<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// Creates one context instance bound to the host database. Callers own
    /// the instance and dispose it (e.g. <c>await using</c>).
    /// </summary>
    TContext CreateDbContext();

    /// <summary>
    /// Applies the context's pending EF migrations under a database advisory
    /// lock. Call this once as the first step of the plugin's StartAsync so
    /// the plugin's schema exists before it is used.
    /// </summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
