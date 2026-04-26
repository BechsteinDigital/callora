namespace Callora.Host.Backend.Infrastructure.Events;

internal sealed record DispatchHandler<TEvent>(
    int Priority,
    Func<TEvent, CancellationToken, Task> Callback);
