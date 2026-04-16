using Callora.Host.Backend.Domain.Audit;
using Callora.Host.Backend.Domain.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class HostPersistenceDbContext(DbContextOptions<HostPersistenceDbContext> options) : DbContext(options)
{
    public DbSet<PluginInstallation> PluginInstallations => Set<PluginInstallation>();

    public DbSet<PluginAuditLog> PluginAuditLogs => Set<PluginAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PluginInstallation>(entity =>
        {
            entity.ToTable("plugin_installations");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PluginId).IsUnique();

            entity.Property(x => x.PluginId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.AssemblyPath).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.EntryTypeName).HasMaxLength(800);
            entity.Property(x => x.State).HasConversion<int>().IsRequired();
            entity.Property(x => x.InstalledAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<PluginAuditLog>(entity =>
        {
            entity.ToTable("plugin_audit_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.PluginId);

            entity.Property(x => x.Action).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PluginId).HasMaxLength(200);
            entity.Property(x => x.RequestedBy).HasMaxLength(200);
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.Property(x => x.MetadataJson).HasColumnType("TEXT");
        });
    }
}
