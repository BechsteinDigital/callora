using Callora.Core.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceSurfaceEntityTypeConfiguration : IEntityTypeConfiguration<WorkspaceSurface>
{
    public void Configure(EntityTypeBuilder<WorkspaceSurface> builder)
    {
        builder.ToTable("workspace_surfaces");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.WorkspaceId, x.SurfaceKey }).IsUnique();
        builder.HasIndex(x => x.PublicHost);

        builder.HasIndex(x => new { x.ParentSurfaceId, x.Position });

        builder.Property(x => x.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(x => x.ParentSurfaceId).HasColumnName("parent_surface_id");
        builder.Property(x => x.Position).HasColumnName("position").IsRequired();
        builder.Property(x => x.RequiredClaims).HasColumnName("required_claims").HasMaxLength(1000);
        builder.Property(x => x.SurfaceKey).HasColumnName("surface_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(300).IsRequired();
        builder.Property(x => x.SurfaceType).HasColumnName("surface_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.PublicBaseUrl).HasColumnName("public_base_url").HasMaxLength(2048);
        builder.Property(x => x.PublicHost).HasColumnName("public_host").HasMaxLength(500);
        builder.Property(x => x.PublicPathPrefix).HasColumnName("public_path_prefix").HasMaxLength(500).IsRequired();
        builder.Property(x => x.AccessMode).HasColumnName("access_mode").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Routing).HasColumnName("routing").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(40);
        builder.Property(x => x.TemplatePluginId).HasColumnName("template_plugin_id").HasMaxLength(200);
        builder.Property(x => x.TemplateVersion).HasColumnName("template_version").HasMaxLength(80);
        builder.Property(x => x.ThemePluginId).HasColumnName("theme_plugin_id").HasMaxLength(200);
        builder.Property(x => x.ThemeVersion).HasColumnName("theme_version").HasMaxLength(80);
        builder.Property(x => x.ThemeAssignedBy).HasColumnName("theme_assigned_by").HasMaxLength(200);
        builder.Property(x => x.ThemeAssignedAtUtc).HasColumnName("theme_assigned_at_utc");
        builder.Property(x => x.IdentityPluginId).HasColumnName("identity_plugin_id").HasMaxLength(200);
        builder.Property(x => x.IdentityVersion).HasColumnName("identity_version").HasMaxLength(80);
        builder.Property(x => x.IdentityAssignedBy).HasColumnName("identity_assigned_by").HasMaxLength(200);
        builder.Property(x => x.IdentityAssignedAtUtc).HasColumnName("identity_assigned_at_utc");
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder
            .HasOne(x => x.Workspace)
            .WithMany(x => x.Surfaces)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, nicht Cascade: Was mit den Kindern eines gelöschten Knotens geschieht, ist
        // eine Produktentscheidung (ADR-019 §7) — sie an den Großelternknoten zu hängen ändert
        // stillschweigend URLs, sie mitzulöschen verliert Layouts. Bis jemand danach fragt,
        // scheitert das Löschen sichtbar, statt still das eine oder andere zu tun.
        builder
            .HasOne(x => x.Parent)
            .WithMany()
            .HasForeignKey(x => x.ParentSurfaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
