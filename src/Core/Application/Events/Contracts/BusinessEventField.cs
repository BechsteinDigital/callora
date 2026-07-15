namespace Callora.Core.Application.Events.Contracts;

/// <summary>
/// One field available on a business event — name, type and a short label
/// for the flow-builder / webhook UI.
/// </summary>
public sealed record BusinessEventField(
    string Name,
    BusinessEventFieldType Type,
    string Label);
