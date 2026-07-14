using Callora.Host.Backend.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence.Configurations;

public sealed class MediaItemEntityTypeConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("media_items");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.WorkspaceKey, x.Folder });

        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(400).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Folder).HasMaxLength(120).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
    }
}
