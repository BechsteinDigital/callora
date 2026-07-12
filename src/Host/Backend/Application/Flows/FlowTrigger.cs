using System.Text.Json;
using Callora.Host.Backend.Application.Abstractions.Flows;
using Callora.Host.Backend.Application.Communication.Calls;
using Callora.Host.PluginContracts.Application.Flows;
using Callora.Host.PluginContracts.Application.Jobs;

namespace Callora.Host.Backend.Application.Flows;

/// <summary>
/// Listens to live call events, matches active flows (trigger + conditions)
/// and enqueues one durable "flow.execute" job per match.
/// </summary>
public sealed class FlowTrigger(
    CallEventBroadcaster broadcaster,
    IServiceScopeFactory scopeFactory,
    IBackgroundJobQueue jobQueue,
    RuleEvaluator ruleEvaluator,
    ILogger<FlowTrigger> logger) : IHostedService
{
    public const string ExecuteJobType = "flow.execute";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        broadcaster.EventPublished += HandleCallEvent;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        broadcaster.EventPublished -= HandleCallEvent;
        return Task.CompletedTask;
    }

    private void HandleCallEvent(CallEvent callEvent)
    {
        _ = DispatchAsync(callEvent);
    }

    private async Task DispatchAsync(CallEvent callEvent)
    {
        try
        {
            IReadOnlyList<FlowSnapshot> flows;
            using (var scope = scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IFlowStore>();
                flows = await store
                    .ListActiveForTriggerAsync(callEvent.Type, callEvent.Call.WorkspaceKey)
                    .ConfigureAwait(false);
            }

            if (flows.Count == 0)
            {
                return;
            }

            var context = BuildContext(callEvent);
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
                            ExecuteJobType,
                            payload,
                            MaxAttempts: 1,
                            WorkspaceKey: flow.WorkspaceKey),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Flow dispatch for event {EventName} failed.", callEvent.Type);
        }
    }

    private static RuleConditionNode? ParseConditions(FlowSnapshot flow) =>
        string.IsNullOrWhiteSpace(flow.ConditionsJson)
            ? null
            : JsonSerializer.Deserialize<RuleConditionNode>(flow.ConditionsJson, JsonOptions);

    internal static RuleContext BuildContext(CallEvent callEvent) => new(
        callEvent.Type,
        callEvent.Call.WorkspaceKey,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["callId"] = callEvent.Call.CallId,
            ["channelId"] = callEvent.Call.ChannelId,
            ["direction"] = callEvent.Call.Direction,
            ["state"] = callEvent.Call.State,
            ["target"] = callEvent.Call.TargetValue
        },
        DateTimeOffset.UtcNow);
}
