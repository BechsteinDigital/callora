using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Application.Events;

public interface IHostApplicationEvent : IHostEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
