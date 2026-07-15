using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Events;

public interface IHostApplicationEvent : IHostEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
