using System.Text.Json;
using Callora.Core.Application.Flows;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Core.Application.Flows;

/// <summary>
/// Executes the action list of one matched flow sequentially. Unknown action
/// types fail loudly so misconfigured flows surface in the job log.
/// </summary>
public sealed class FlowExecuteJobHandler(
    IFlowStore flowStore,
    FlowActionRegistry actionRegistry,
    ILogger<FlowExecuteJobHandler> logger) : IBackgroundJobHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string JobType => FlowJobs.ExecuteJobType;

    public async Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<FlowExecutePayload>(context.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Flow execution payload could not be parsed.");

        var flow = await flowStore.GetAsync(payload.FlowId, cancellationToken).ConfigureAwait(false);
        if (flow is null || !flow.IsActive)
        {
            return;
        }

        var steps = JsonSerializer.Deserialize<FlowActionStep[]>(flow.ActionsJson, JsonOptions) ?? [];
        var ruleContext = new RuleContext(
            payload.EventName,
            payload.WorkspaceKey,
            payload.Data,
            DateTimeOffset.UtcNow);

        foreach (var step in steps)
        {
            var handler = actionRegistry.Resolve(step.Type)
                ?? throw new InvalidOperationException(
                    $"Flow '{flow.Name}' references unknown action type '{step.Type}'.");

            logger.LogInformation(
                "Flow {FlowName} executes action {ActionType} for event {EventName}.",
                flow.Name,
                step.Type,
                payload.EventName);

            await handler
                .ExecuteAsync(ruleContext, step.Params ?? new Dictionary<string, string>(), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
