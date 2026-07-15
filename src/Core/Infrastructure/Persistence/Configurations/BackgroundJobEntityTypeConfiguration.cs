using Callora.Core.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class BackgroundJobEntityTypeConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("background_jobs");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
        builder.HasIndex(x => new { x.Status, x.LeaseExpiresAtUtc });
        builder.HasIndex(x => x.JobType);

        builder.Property(x => x.JobType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.MaxAttempts).IsRequired();
        builder.Property(x => x.ScheduledAtUtc).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);
        // Fencing token: a reclaimed job's previous owner must fail to save.
        builder.Property(x => x.LeaseToken).IsConcurrencyToken();
    }
}
