using Callora.Core.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class BackendRbacRoleEntityTypeConfiguration : IEntityTypeConfiguration<BackendRbacRole>
{
    public void Configure(EntityTypeBuilder<BackendRbacRole> builder)
    {
        builder.ToTable("backend_rbac_roles");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ProvisionedByPluginId).HasMaxLength(128);
        builder.Property(x => x.ProvisionedAs).HasMaxLength(64);

        // Eindeutig, damit zwei gleichzeitig startende Knoten nicht beide dieselbe Rolle anlegen.
        //
        // Der Filter ist NICHT das, was von Hand erstellte Rollen nebeneinander erlaubt — das tut
        // Postgres ohnehin, weil NULL-Werte in einem Unique-Index als verschieden gelten (hier
        // nachgemessen: ohne Filter läuft derselbe Fall genauso durch). Er ist da, damit der Index nur
        // die Zeilen enthält, um die es geht: bereitgestellte Rollen sind eine Handvoll, von Hand
        // erstellte können viele sein, und alle mitzuführen hieße, ein NULL-Paar pro Zeile zu indizieren,
        // nach dem nie jemand sucht.
        builder.HasIndex(x => new { x.ProvisionedByPluginId, x.ProvisionedAs })
            .IsUnique()
            .HasFilter("\"ProvisionedByPluginId\" IS NOT NULL");
        builder.Property(x => x.IsSystem).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasMany(x => x.Permissions)
            .WithOne(x => x.Role)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.UserAssignments)
            .WithOne(x => x.Role)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
