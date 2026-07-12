using Callora.Host.Backend.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class NotificationEntryEntityTypeConfiguration : IEntityTypeConfiguration<NotificationEntry>
{
    public void Configure(EntityTypeBuilder<NotificationEntry> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.WorkspaceKey, x.IsRead, x.CreatedAtUtc });

        builder.Property(x => x.WorkspaceKey).HasMaxLength(120);
        builder.Property(x => x.Title).HasMaxLength(240).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Level).HasMaxLength(20).IsRequired();
    }
}
