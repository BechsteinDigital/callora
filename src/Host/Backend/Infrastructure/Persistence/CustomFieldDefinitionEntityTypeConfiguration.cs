using Callora.Host.Backend.Domain.CustomFields;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class CustomFieldDefinitionEntityTypeConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("custom_field_definitions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EntityName, x.FieldKey }).IsUnique();

        builder.Property(x => x.PluginId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(60).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(60).IsRequired();
        builder.Property(x => x.FieldKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(240).IsRequired();
        builder.Property(x => x.FieldType).HasMaxLength(40).IsRequired();
    }
}
