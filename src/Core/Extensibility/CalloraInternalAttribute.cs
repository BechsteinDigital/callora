namespace Callora.Core.Extensibility;

/// <summary>
/// Marks a public type or member that is visible for technical reasons only and is
/// <b>not</b> part of the stable Callora plugin contract. Consuming it from outside the
/// framework assemblies is reported as <c>CAL0001</c> by the Callora analyzer (REV2 §7.1).
/// </summary>
/// <remarks>
/// Use this where a type must be <c>public</c> for composition or serialization to work,
/// but is not intended as an extension point. For genuinely non-visible members prefer
/// <c>internal</c>; reserve this marker for the surface that cannot be hidden.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface |
    AttributeTargets.Enum | AttributeTargets.Delegate | AttributeTargets.Method |
    AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Field |
    AttributeTargets.Event,
    Inherited = false,
    AllowMultiple = false)]
public sealed class CalloraInternalAttribute : Attribute
{
    /// <summary>Marks the target as internal to the framework with no stated reason.</summary>
    public CalloraInternalAttribute()
    {
    }

    /// <summary>Marks the target as internal to the framework and records why.</summary>
    /// <param name="reason">Human-readable justification shown in the CAL0001 message.</param>
    public CalloraInternalAttribute(string reason) => Reason = reason;

    /// <summary>Optional justification surfaced to consumers that trip CAL0001.</summary>
    public string? Reason { get; }
}
