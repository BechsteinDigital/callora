using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Flows;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Jobs.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Callora.Core.Application.Events.Business;

/// <summary>
/// Routes every business event into the flow engine (PLAT-270): matches
/// active flows by trigger name and conditions, then enqueues one durable
/// flow.execute job per match. Replaces the call-specific FlowTrigger
/// binding with a generic subscriber — any event can trigger a flow.
/// </summary>
public sealed class FlowBusinessEventListener(
    IServiceScopeFactory scopeFactory,
    IBackgroundJobQueue jobQueue,
    RuleEvaluator ruleEvaluator,
    ILogger<FlowBusinessEventListener> logger) : IBusinessEventListener
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Priority => 0;

    public async Task OnBusinessEventAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<FlowSnapshot> flows;
            using (var scope = scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IFlowStore>();
                flows = await store
                    .ListActiveForTriggerAsync(businessEvent.EventName, businessEvent.WorkspaceKey ?? string.Empty)
                    .ConfigureAwait(false);
            }

            if (flows.Count == 0)
            {
                return;
            }

            var context = new RuleContext(
                businessEvent.EventName,
                businessEvent.WorkspaceKey,
                businessEvent.ToEventData(),
                DateTimeOffset.UtcNow);

            foreach (var flow in flows.OrderBy(static flow => flow.Priority))
            {
                var conditions = ParseConditions(flow);
                if (!ruleEvaluator.Evaluate(conditions, context))
                {
                    continue;
                }

                var payload = JsonSerializer.Serialize(
                    new FlowExecutePayload(flow.Id, context.EventName, context.WorkspaceKey, new Dictionary<string, string>(context.Data)),
                    JsonOptions);
                await jobQueue.EnqueueAsync(
                        new BackgroundJobRequest(
                            FlowJobs.ExecuteJobType,
                            payload,
                            MaxAttempts: 1,
                            WorkspaceKey: flow.WorkspaceKey),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Flow dispatch for business event {EventName} failed.", businessEvent.EventName);
        }
    }

    private static RuleConditionNode? ParseConditions(FlowSnapshot flow) =>
        string.IsNullOrWhiteSpace(flow.ConditionsJson)
            ? null
            : JsonSerializer.Deserialize<RuleConditionNode>(flow.ConditionsJson, JsonOptions);
}
