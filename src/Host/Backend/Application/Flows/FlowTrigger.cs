using System.Text.Json;
using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Abstractions.Flows;
using Callora.Host.PluginContracts.Application.Flows;
using Callora.Host.PluginContracts.Application.Jobs;
using Callora.Hosting.Application.Plugins;

namespace Callora.Host.Backend.Application.Flows;

/// <summary>
/// Listens to live call events exported by communication plugins
/// (<see cref="ICallEventStream"/>), matches active flows (trigger +
/// conditions) and enqueues one durable "flow.execute" job per match. The
/// host holds no call logic — it binds to the exported streams and rebinds
/// when plugins activate or deactivate (PLAT-257).
/// </summary>
public sealed class FlowTrigger(
    ICalloraPluginCatalog pluginCatalog,
    IServiceScopeFactory scopeFactory,
    IBackgroundJobQueue jobQueue,
    RuleEvaluator ruleEvaluator,
    ILogger<FlowTrigger> logger) : IHostedService
{
    public const string ExecuteJobType = "flow.execute";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object _bindingLock = new();
    private readonly HashSet<ICallEventStream> _boundStreams = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        RefreshBindings();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_bindingLock)
        {
            foreach (var stream in _boundStreams)
            {
                stream.EventPublished -= HandleCallEvent;
            }

            _boundStreams.Clear();
        }

        return Task.CompletedTask;
    }

    /// <summary>Binds to newly exported streams and drops vanished ones.</summary>
    public void RefreshBindings()
    {
        var current = pluginCatalog.GetExports<ICallEventStream>().ToHashSet();
        lock (_bindingLock)
        {
            foreach (var stale in _boundStreams.Where(stream => !current.Contains(stream)).ToArray())
            {
                stale.EventPublished -= HandleCallEvent;
                _boundStreams.Remove(stale);
            }

            foreach (var stream in current.Where(stream => !_boundStreams.Contains(stream)))
            {
                stream.EventPublished += HandleCallEvent;
                _boundStreams.Add(stream);
            }
        }
    }

    private void HandleCallEvent(CallStreamEvent callEvent)
    {
        _ = DispatchAsync(callEvent);
    }

    private async Task DispatchAsync(CallStreamEvent callEvent)
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

    internal static RuleContext BuildContext(CallStreamEvent callEvent) => new(
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
