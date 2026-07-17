namespace Callora.Core.Application.Events.Contracts;

/// <summary>
/// Describes one business event for discovery: its name, an owner label and
/// the fields it exposes. Plugins export descriptors so the flow-builder and
/// webhook UI know which events exist and what data they carry — the Callora
/// counterpart of Shopware's BusinessEventDefinition.
/// </summary>
/// <param name="EventName">Stable event name plugins and flows key on.</param>
/// <param name="DisplayName">Human-readable event name for the UI.</param>
/// <param name="Fields">The fields this event exposes.</param>
public sealed record BusinessEventDescriptor(
    string EventName,
    string DisplayName,
    IReadOnlyList<BusinessEventField> Fields);
