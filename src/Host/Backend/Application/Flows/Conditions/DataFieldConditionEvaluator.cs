using Callora.Host.PluginContracts.Application.Flows;

namespace Callora.Host.Backend.Application.Flows.Conditions;

/// <summary>
/// Matches one context data field ("field") against a pattern ("value"):
/// exact match, or prefix match with a trailing "*".
/// Covers call.direction, call.state, call.target etc. without one class per field.
/// </summary>
public sealed class DataFieldConditionEvaluator : IRuleConditionEvaluator
{
    public string Type => "data.field";

    public bool Evaluate(RuleContext context, IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("field", out var field) ||
            !parameters.TryGetValue("value", out var pattern) ||
            string.IsNullOrWhiteSpace(field))
        {
            return false;
        }

        if (!context.Data.TryGetValue(field.Trim(), out var actual))
        {
            return false;
        }

        var trimmedPattern = pattern?.Trim() ?? string.Empty;
        return trimmedPattern.EndsWith('*')
            ? actual.StartsWith(trimmedPattern[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(actual, trimmedPattern, StringComparison.OrdinalIgnoreCase);
    }
}
