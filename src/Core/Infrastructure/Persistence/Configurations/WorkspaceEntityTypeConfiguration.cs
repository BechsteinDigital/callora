using Callora.Core.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEntity = Callora.Core.Domain.Workspaces.Workspace;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceEntityTypeConfiguration : IEntityTypeConfiguration<WorkspaceEntity>
{
    public void Configure(EntityTypeBuilder<WorkspaceEntity> builder)
    {
        builder.ToTable("workspaces");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.WorkspaceKey).IsUnique();

        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.WorkspaceType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.ThemePluginId).HasColumnName("theme_plugin_id").HasMaxLength(200);
        builder.Property(x => x.ThemeVersion).HasColumnName("theme_version").HasMaxLength(80);
        builder.Property(x => x.ThemeAssignedBy).HasColumnName("theme_assigned_by").HasMaxLength(200);
        builder.Property(x => x.ThemeAssignedAtUtc).HasColumnName("theme_assigned_at_utc");
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Property(x => x.TenantId).IsRequired();

        builder
            .HasOne(x => x.Tenant)
            .WithMany(x => x.Workspaces)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(x => x.Memberships)
            .WithOne(x => x.Workspace)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
