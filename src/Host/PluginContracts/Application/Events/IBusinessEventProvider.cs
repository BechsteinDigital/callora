namespace Callora.Host.PluginContracts.Application.Events;

/// <summary>
/// Supplies business-event descriptors for discovery. Host subsystems
/// register providers in DI; plugins export them via
/// <c>IHostPluginContext.Export</c>.
/// </summary>
public interface IBusinessEventProvider
{
    /// <summary>Returns the descriptors this provider owns.</summary>
    IReadOnlyList<BusinessEventDescriptor> GetDescriptors();
}
