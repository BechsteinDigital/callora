using Callora.Host.Backend.Domain.CustomFields;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence.Configurations;

public sealed class CustomFieldValueEntityTypeConfiguration : IEntityTypeConfiguration<CustomFieldValue>
{
    public void Configure(EntityTypeBuilder<CustomFieldValue> builder)
    {
        builder.ToTable("custom_field_values");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EntityName, x.EntityId, x.FieldKey }).IsUnique();

        builder.Property(x => x.EntityName).HasMaxLength(60).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FieldKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ValueJson).IsRequired();
        builder.Property(x => x.WorkspaceKey).HasMaxLength(120);
        builder.HasIndex(x => x.WorkspaceKey);
    }
}
