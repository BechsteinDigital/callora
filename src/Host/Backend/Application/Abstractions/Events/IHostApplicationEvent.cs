namespace Callora.Host.Backend.Application.Abstractions.Events;

public interface IHostApplicationEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
