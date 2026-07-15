using Callora.Core.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceMembershipEntityTypeConfiguration : IEntityTypeConfiguration<WorkspaceMembership>
{
    public void Configure(EntityTypeBuilder<WorkspaceMembership> builder)
    {
        builder.ToTable("workspace_memberships");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.WorkspaceId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.Property(x => x.Role).HasMaxLength(120).IsRequired();
        builder.Property(x => x.AssignedAtUtc).IsRequired();

        builder
            .HasOne(x => x.User)
            .WithMany(x => x.WorkspaceMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
