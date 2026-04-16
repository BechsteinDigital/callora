namespace Callora.Modules.Abstractions.Domain.FeatureFlags;

/// <summary>
/// Describes a module feature that can be gated by licensing.
/// </summary>
public interface ICalloraFeatureDescriptor
{
    /// <summary>Unique feature key.</summary>
    string FeatureKey { get; }

    /// <summary>Human-readable feature description.</summary>
    string Description { get; }
}
