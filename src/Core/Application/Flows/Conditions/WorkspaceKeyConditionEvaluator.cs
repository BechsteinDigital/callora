using Callora.Core.Application.Flows.Contracts;

namespace Callora.Core.Application.Flows.Conditions;

/// <summary>Matches the workspace key of the triggering event ("value").</summary>
public sealed class WorkspaceKeyConditionEvaluator : IRuleConditionEvaluator
{
    public string Type => "workspace.key";

    public bool Evaluate(RuleContext context, IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("value", out var value) &&
        string.Equals(context.WorkspaceKey, value?.Trim(), StringComparison.OrdinalIgnoreCase);
}
