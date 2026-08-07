using Callora.Plugin.Composer.Domain;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Composer.Infrastructure.Persistence;

/// <summary>
/// The Composer's own database. Its tables live in the dedicated <c>plugin_composer</c> schema on
/// the shared host database, so the plugin owns its data with real entities and migrations — and
/// the host can drop the schema cleanly when it is uninstalled.
/// </summary>
public sealed class ComposerDbContext(DbContextOptions<ComposerDbContext> options) : DbContext(options)
{
    /// <summary>Dedicated Postgres schema for this plugin.</summary>
    public const string SchemaName = "plugin_composer";

    public DbSet<SurfaceLayout> Layouts => Set<SurfaceLayout>();

    public DbSet<SurfaceLayoutVersion> Versions => Set<SurfaceLayoutVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<SurfaceLayout>(entity =>
        {
            entity.ToTable("surface_layouts");
            entity.HasKey(layout => layout.Key);
            entity.Property(layout => layout.Key).HasColumnName("key").HasMaxLength(128);
            entity.Property(layout => layout.WorkspaceKey).HasColumnName("workspace_key").HasMaxLength(128);
            entity.Property(layout => layout.SurfaceKey).HasColumnName("surface_key").HasMaxLength(128);
            entity.Property(layout => layout.Name).HasColumnName("name").HasMaxLength(256);

            // Der Renderpfad fragt "welches Layout gehört zu dieser Fläche" bei JEDEM Aufruf.
            entity.HasIndex(layout => new { layout.WorkspaceKey, layout.SurfaceKey });
        });

        modelBuilder.Entity<SurfaceLayoutVersion>(entity =>
        {
            entity.ToTable("surface_layout_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.Id).HasColumnName("id");
            entity.Property(version => version.LayoutKey).HasColumnName("layout_key").HasMaxLength(128);
            entity.Property(version => version.VersionNumber).HasColumnName("version_number");
            entity.Property(version => version.State).HasColumnName("state").HasConversion<string>().HasMaxLength(16);
            entity.Property(version => version.Document).HasColumnName("document").HasColumnType("jsonb");
            entity.Property(version => version.Label).HasColumnName("label").HasMaxLength(256);
            entity.Property(version => version.CreatedBy).HasColumnName("created_by").HasMaxLength(256);
            entity.Property(version => version.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(version => version.ChangedAtUtc).HasColumnName("changed_at_utc");
            entity.Property(version => version.PublishedBy).HasColumnName("published_by").HasMaxLength(256);
            entity.Property(version => version.PublishedAtUtc).HasColumnName("published_at_utc");

            entity.HasIndex(version => new { version.LayoutKey, version.State });
            entity.HasIndex(version => new { version.LayoutKey, version.VersionNumber }).IsUnique();
        });
    }
}
