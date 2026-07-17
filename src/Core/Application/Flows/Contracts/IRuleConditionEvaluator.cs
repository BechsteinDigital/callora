using Callora.Core.Extensibility;

namespace Callora.Core.Application.Flows.Contracts;

/// <summary>
/// One leaf condition type of the rule system. Plugins export additional
/// evaluators via IHostPluginContext.Export&lt;IRuleConditionEvaluator&gt;.
/// </summary>
[CalloraExtensible("Extension point — implement and export to contribute a rule condition (REV2 §8.2)")]
public interface IRuleConditionEvaluator
{
    /// <summary>Condition type key, e.g. "call.direction" or "time.window".</summary>
    string Type { get; }

    /// <summary>
    /// Evaluates the condition against the rule context and its configured parameters.
    /// </summary>
    bool Evaluate(RuleContext context, IReadOnlyDictionary<string, string> parameters);
}
