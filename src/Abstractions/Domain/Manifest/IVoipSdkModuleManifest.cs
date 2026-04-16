using Callora.Modules.Abstractions.Domain.FeatureFlags;

namespace Callora.Modules.Abstractions.Domain.Manifest;

/// <summary>
/// Describes metadata and exposed features of a module.
/// </summary>
public interface ICalloraModuleManifest
{
    /// <summary>Stable module identifier.</summary>
    string ModuleId { get; }

    /// <summary>List of feature descriptors exposed by the module.</summary>
    IReadOnlyList<ICalloraFeatureDescriptor> Features { get; }
}
