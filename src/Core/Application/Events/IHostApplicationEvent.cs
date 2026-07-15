using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Core.Application.Events;

public interface IHostApplicationEvent : IHostEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
