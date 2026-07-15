using Callora.Core.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Core.Infrastructure.Persistence.Configurations;

public sealed class PluginAuditLogEntityTypeConfiguration : IEntityTypeConfiguration<PluginAuditLog>
{
    public void Configure(EntityTypeBuilder<PluginAuditLog> builder)
    {
        builder.ToTable("plugin_audit_logs");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.PluginId);

        builder.Property(x => x.Action).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PluginId).HasMaxLength(200);
        builder.Property(x => x.RequestedBy).HasMaxLength(200);
        builder.Property(x => x.Message).HasMaxLength(2000);
        builder.Property(x => x.MetadataJson).HasColumnType("TEXT");
    }
}
