namespace Callora.Core.Application.Events.Contracts;

/// <summary>
/// One field available on a business event — name, type and a short label
/// for the flow-builder / webhook UI.
/// </summary>
/// <param name="Name">Field key as it appears in the event data bag.</param>
/// <param name="Type">Value type of the field.</param>
/// <param name="Label">Short human-readable label for the UI.</param>
public sealed record BusinessEventField(
    string Name,
    BusinessEventFieldType Type,
    string Label);
