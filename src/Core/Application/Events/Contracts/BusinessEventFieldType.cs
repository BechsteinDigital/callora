namespace Callora.Core.Application.Events.Contracts;

/// <summary>Value type of one business-event field in the schema.</summary>
public enum BusinessEventFieldType
{
    /// <summary>Free-form text.</summary>
    Text = 0,

    /// <summary>Integer number.</summary>
    Number = 1,

    /// <summary>Boolean flag.</summary>
    Boolean = 2,

    /// <summary>ISO-8601 timestamp.</summary>
    Timestamp = 3
}
