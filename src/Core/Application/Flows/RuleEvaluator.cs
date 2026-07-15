using Callora.Host.PluginContracts.Application.Flows;

namespace Callora.Core.Application.Flows;

/// <summary>
/// Evaluates a condition tree against a rule context. Unknown leaf types
/// evaluate to false and are reported so misconfigured flows never match
/// silently as true.
/// </summary>
public sealed class RuleEvaluator(IEnumerable<IRuleConditionEvaluator> evaluators, ILogger<RuleEvaluator> logger)
{
    private readonly Dictionary<string, IRuleConditionEvaluator> _byType = evaluators
        .GroupBy(evaluator => evaluator.Type, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

    public bool Evaluate(RuleConditionNode? node, RuleContext context)
    {
        if (node is null)
        {
            // No conditions means the flow always matches its trigger event.
            return true;
        }

        var children = node.Children ?? [];
        return node.Type.ToLowerInvariant() switch
        {
            "and" => children.All(child => Evaluate(child, context)),
            "or" => children.Any(child => Evaluate(child, context)),
            "not" => children.Length == 1
                ? !Evaluate(children[0], context)
                : throw new InvalidOperationException("'not' requires exactly one child condition."),
            _ => EvaluateLeaf(node, context)
        };
    }

    private bool EvaluateLeaf(RuleConditionNode node, RuleContext context)
    {
        if (!_byType.TryGetValue(node.Type, out var evaluator))
        {
            logger.LogWarning("Unknown rule condition type '{ConditionType}' evaluates to false.", node.Type);
            return false;
        }

        return evaluator.Evaluate(context, node.Params ?? new Dictionary<string, string>());
    }
}
