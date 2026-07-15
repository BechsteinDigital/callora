namespace Callora.Core.Application.Events.Contracts;

/// <summary>
/// Describes one business event for discovery: its name, an owner label and
/// the fields it exposes. Plugins export descriptors so the flow-builder and
/// webhook UI know which events exist and what data they carry — the Callora
/// counterpart of Shopware's BusinessEventDefinition.
/// </summary>
public sealed record BusinessEventDescriptor(
    string EventName,
    string DisplayName,
    IReadOnlyList<BusinessEventField> Fields);
