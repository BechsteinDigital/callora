using Callora.Core.Extensibility;

namespace Callora.Core.Application.Flows.Contracts;

/// <summary>
/// One executable flow action type. Plugins export additional actions via
/// IHostPluginContext.Export&lt;IFlowActionHandler&gt;.
/// </summary>
[CalloraExtensible("Extension point — implement and export to contribute a flow action (REV2 §8.2)")]
public interface IFlowActionHandler
{
    /// <summary>Action type key, e.g. "call.accept" or "audio.play".</summary>
    string Type { get; }

    /// <summary>
    /// Runs the action against the rule context and its configured parameters.
    /// </summary>
    Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default);
}
