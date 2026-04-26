using Callora.Host.Backend.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class BackendRbacRoleGrantEntityTypeConfiguration : IEntityTypeConfiguration<BackendRbacRoleGrant>
{
    public void Configure(EntityTypeBuilder<BackendRbacRoleGrant> builder)
    {
        builder.ToTable("backend_rbac_role_permissions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.RoleId, x.PermissionKey }).IsUnique();

        builder.Property(x => x.PermissionKey).HasMaxLength(120).IsRequired();
    }
}
