using Callora.Core.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class BackendUserEntityTypeConfiguration : IEntityTypeConfiguration<BackendUser>
{
    public void Configure(EntityTypeBuilder<BackendUser> builder)
    {
        builder.ToTable("backend_users");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ExternalId).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.PasswordHash).HasMaxLength(1024);
        builder.Property(x => x.PasswordHashAlgorithm).HasMaxLength(100);
        builder.Property(x => x.DisplayName).HasMaxLength(300);
        builder.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(x => x.IsDisabled).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.FailedAccessCount).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.LockoutEndsAtUtc);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
