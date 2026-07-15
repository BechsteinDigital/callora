using Callora.Core.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class WebhookSubscriptionEntityTypeConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EventName, x.IsActive });

        builder.Property(x => x.WorkspaceKey).HasMaxLength(120);
        builder.Property(x => x.EventName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.TargetUrl).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Secret).HasMaxLength(400).IsRequired();
    }
}
