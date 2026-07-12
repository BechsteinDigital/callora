using Callora.Host.PluginContracts.Application.Flows;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Flow action fake recording executions per action type.
/// </summary>
public sealed class RecordingFlowActionHandler(string type) : IFlowActionHandler
{
    private readonly List<(RuleContext Context, IReadOnlyDictionary<string, string> Parameters)> _executions = [];

    public string Type => type;

    public IReadOnlyList<(RuleContext Context, IReadOnlyDictionary<string, string> Parameters)> Executions => _executions;

    public Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        _executions.Add((context, parameters));
        return Task.CompletedTask;
    }
}
