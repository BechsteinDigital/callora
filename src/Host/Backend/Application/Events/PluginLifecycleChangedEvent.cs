using Callora.Host.Backend.Application.Abstractions.Events;

namespace Callora.Host.Backend.Application.Events;

public sealed record PluginLifecycleChangedEvent(
    DateTimeOffset OccurredAtUtc,
    string Action,
    string? PluginId,
    bool IsSuccess,
    string? RequestedBy,
    string? Message,
    IReadOnlyDictionary<string, string>? Metadata = null) : IHostApplicationEvent;
