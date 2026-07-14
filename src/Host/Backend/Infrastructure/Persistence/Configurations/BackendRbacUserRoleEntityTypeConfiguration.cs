using Callora.Host.Backend.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence.Configurations;

public sealed class BackendRbacUserRoleEntityTypeConfiguration : IEntityTypeConfiguration<BackendRbacUserRole>
{
    public void Configure(EntityTypeBuilder<BackendRbacUserRole> builder)
    {
        builder.ToTable("backend_rbac_user_roles");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId).IsUnique();

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.AssignedAtUtc).IsRequired();

        builder
            .HasOne(x => x.User)
            .WithMany(x => x.RoleAssignments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
