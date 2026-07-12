using Callora.Host.PluginContracts.Application.Flows;

namespace Callora.Host.Backend.Application.Flows.Conditions;

/// <summary>Matches the triggering event name ("value").</summary>
public sealed class EventNameConditionEvaluator : IRuleConditionEvaluator
{
    public string Type => "event.name";

    public bool Evaluate(RuleContext context, IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("value", out var value) &&
        string.Equals(context.EventName, value?.Trim(), StringComparison.OrdinalIgnoreCase);
}
