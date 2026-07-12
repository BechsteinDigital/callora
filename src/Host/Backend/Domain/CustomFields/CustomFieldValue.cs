namespace Callora.Host.Backend.Domain.CustomFields;

/// <summary>
/// One stored custom field value on a concrete entity instance.
/// </summary>
public sealed class CustomFieldValue
{
    public Guid Id { get; set; }

    public string EntityName { get; set; } = string.Empty;

    /// <summary>Identifier of the entity instance, e.g. the workspace key.</summary>
    public string EntityId { get; set; } = string.Empty;

    public string FieldKey { get; set; } = string.Empty;

    public string ValueJson { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
