namespace Callora.Core.Application.Flows.Contracts;

/// <summary>
/// One executable flow action type. Plugins export additional actions via
/// IHostPluginContext.Export&lt;IFlowActionHandler&gt;.
/// </summary>
public interface IFlowActionHandler
{
    /// <summary>Action type key, e.g. "call.accept" or "audio.play".</summary>
    string Type { get; }

    Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default);
}
