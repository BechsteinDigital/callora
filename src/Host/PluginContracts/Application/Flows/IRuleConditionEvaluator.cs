namespace Callora.Host.PluginContracts.Application.Flows;

/// <summary>
/// One leaf condition type of the rule system. Plugins export additional
/// evaluators via IHostPluginContext.Export&lt;IRuleConditionEvaluator&gt;.
/// </summary>
public interface IRuleConditionEvaluator
{
    /// <summary>Condition type key, e.g. "call.direction" or "time.window".</summary>
    string Type { get; }

    bool Evaluate(RuleContext context, IReadOnlyDictionary<string, string> parameters);
}
