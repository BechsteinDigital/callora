using Callora.Plugin.Communication.Domain.Streaming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="MediaStreamSession"/>; the audio format value object is owned.</summary>
public sealed class MediaStreamSessionConfiguration : IEntityTypeConfiguration<MediaStreamSession>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MediaStreamSession> builder)
    {
        builder.ToTable("media_stream_sessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasMaxLength(64);
        builder.Property(x => x.CallId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ConsumerRef).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ConnectToken).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(15).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.OwnsOne(x => x.Format, format =>
        {
            format.Property(p => p.Codec).HasColumnName("audio_codec").HasConversion<string>().HasMaxLength(20).IsRequired();
            format.Property(p => p.SampleRateHz).HasColumnName("audio_sample_rate_hz").IsRequired();
            format.Property(p => p.FrameMilliseconds).HasColumnName("audio_frame_ms").IsRequired();
        });

        // Single-use connect token → unique lookup key for WS-connect authorization. Atomic
        // single-use under a concurrent double-connect is enforced by a conditional UPDATE in the
        // store (EfMediaStreamSessionStore.TryActivateByConnectTokenAsync), not a mapping concern.
        builder.HasIndex(x => x.ConnectToken).IsUnique();
        builder.HasIndex(x => x.WorkspaceKey);
    }
}
