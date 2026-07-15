namespace Callora.Core.Application.Flows;

/// <summary>
/// One node of the JSON condition tree: "and"/"or"/"not" combinators carry
/// children; every other type is a leaf resolved via the evaluator registry.
/// </summary>
public sealed record RuleConditionNode(
    string Type,
    RuleConditionNode[]? Children = null,
    Dictionary<string, string>? Params = null);
