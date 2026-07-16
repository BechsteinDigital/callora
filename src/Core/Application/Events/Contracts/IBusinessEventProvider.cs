using Callora.Core.Extensibility;

namespace Callora.Core.Application.Events.Contracts;

/// <summary>
/// Supplies business-event descriptors for discovery. Host subsystems
/// register providers in DI; plugins export them via
/// <c>IHostPluginContext.Export</c>.
/// </summary>
[CalloraExtensible("Extension point — implement/export to contribute business events (REV2 §8.2)")]
public interface IBusinessEventProvider
{
    /// <summary>Returns the descriptors this provider owns.</summary>
    IReadOnlyList<BusinessEventDescriptor> GetDescriptors();
}
