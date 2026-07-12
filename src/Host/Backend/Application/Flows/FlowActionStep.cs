namespace Callora.Host.Backend.Application.Flows;

/// <summary>One entry of a flow's action list.</summary>
public sealed record FlowActionStep(
    string Type,
    Dictionary<string, string>? Params = null);
