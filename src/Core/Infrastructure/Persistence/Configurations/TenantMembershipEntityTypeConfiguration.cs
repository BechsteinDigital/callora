using Callora.Core.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class TenantMembershipEntityTypeConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tenant_memberships");
        builder.HasKey(x => x.Id);

        // Eine Person gehört einem Mandanten einmal an. Zwei Zeilen wären zwei Antworten auf die
        // Frage, welche Rolle bei der Anmeldung gilt, und die Auflösung entschiede still und immer
        // gleich, welche gewinnt.
        builder.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.Role).HasMaxLength(120).IsRequired();
        builder.Property(x => x.AssignedAtUtc).IsRequired();

        builder
            .HasOne(x => x.Tenant)
            .WithMany(x => x.Memberships)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.User)
            .WithMany(x => x.TenantMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
