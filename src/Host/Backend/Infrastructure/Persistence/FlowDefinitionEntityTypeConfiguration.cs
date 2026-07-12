using Callora.Host.Backend.Domain.Flows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class FlowDefinitionEntityTypeConfiguration : IEntityTypeConfiguration<FlowDefinition>
{
    public void Configure(EntityTypeBuilder<FlowDefinition> builder)
    {
        builder.ToTable("flows");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.WorkspaceKey, x.TriggerEvent, x.IsActive });

        builder.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(240).IsRequired();
        builder.Property(x => x.TriggerEvent).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ActionsJson).IsRequired();
    }
}
