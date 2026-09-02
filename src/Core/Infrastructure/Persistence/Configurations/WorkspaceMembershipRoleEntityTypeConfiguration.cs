using Callora.Core.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceMembershipRoleEntityTypeConfiguration
    : IEntityTypeConfiguration<WorkspaceMembershipRole>
{
    public void Configure(EntityTypeBuilder<WorkspaceMembershipRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("workspace_membership_roles");
        builder.HasKey(x => x.Id);

        // Dieselbe Rolle zweimal an dieselbe Mitgliedschaft wäre keine zweite Berechtigung, sondern
        // eine zweite Zeile, die beim Entziehen übrig bleibt.
        builder.HasIndex(x => new { x.MembershipId, x.RoleId }).IsUnique();

        builder.Property(x => x.AssignedAtUtc).IsRequired();

        builder
            .HasOne(x => x.Membership)
            .WithMany(x => x.Roles)
            .HasForeignKey(x => x.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kaskade auch von der Rolle her, und das ist eine Entscheidung: Die globale Zuweisung
        // (backend_rbac_user_roles) verwendet Restrict, aber dort löscht niemand eine Rolle über den
        // Endpunkt — hier täte er es, und Restrict machte aus einem gewollten Löschen einen
        // DbUpdateException-500, den kein Betreiber lesen kann. Die Zuweisung ohne ihre Rolle bedeutet
        // ohnehin nichts. Wer die Rolle löscht, sieht es an den Sitzungen, die dabei widerrufen werden.
        builder
            .HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
