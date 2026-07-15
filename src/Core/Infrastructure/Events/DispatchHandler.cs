namespace Callora.Core.Infrastructure.Events;

internal sealed record DispatchHandler<TEvent>(
    int Priority,
    Func<TEvent, CancellationToken, Task> Callback);
