# Flows

A **flow** is an operator-built automation: *when a trigger event fires and the conditions match,
run this list of actions*. Flows are the low-code layer of Callora — call routing, business
automation, notifications — composed from a vocabulary your plugin supplies. [Rules](./rules)
cover the *when* (conditions); this page covers the *do*: **flow actions**.

Your plugin implements an `IFlowActionHandler`, exports it, and from then on operators can drop
your action into any flow via `/api/flows`. When a flow runs, the host invokes your action with
its configured parameters and the triggering event's context.

The worked references are the Communication plugin's call actions
(`custom/static-plugins/Communication/src/Application/Flows/`): `CallAcceptActionHandler`
(`call.accept`), `CallRejectActionHandler` (`call.reject`), `AudioPlayActionHandler`
(`audio.play`), plus the core `webhook.send` action.

## What you'll learn

- The flow model: trigger → conditions → ordered actions (`FlowDefinition`)
- How to implement and export an `IFlowActionHandler`
- How an action is invoked — its config `parameters` and the `RuleContext`
- How flow execution runs as a durable, idempotent background job
- How operators build flows through `POST /api/flows` and `UpsertFlowApiRequest`
- A worked "send an SMS" action

## The flow model

A flow is stored as a `FlowDefinition` (`src/Core/Domain/Flows/FlowDefinition.cs`):

```csharp
public sealed class FlowDefinition
{
    public Guid Id { get; set; }
    public string WorkspaceKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty; // e.g. "call.ringing"
    public string? ConditionsJson { get; set; }              // RuleConditionNode tree; null = always
    public string ActionsJson { get; set; } = "[]";          // [{ "type": "...", "params": { ... } }]
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 100;                 // lower runs first when many match
    // CreatedAtUtc / UpdatedAtUtc …
}
```

The lifecycle end to end:

1. A **business event** fires (e.g. `call.ringing`) — see [Events & jobs](/guides/events-and-jobs).
2. `FlowBusinessEventListener` loads the active flows whose `TriggerEvent` matches, evaluates each
   flow's condition tree ([Rules](./rules)) against a `RuleContext` built from the event, and for
   each match **enqueues a `flow.execute` background job**, ordered by `Priority` (lower first).
3. `FlowExecuteJobHandler` runs that job: it re-loads the flow, deserializes its `ActionsJson` into
   `FlowActionStep`s, and runs each action **sequentially**.

So flows are event-triggered but execute **asynchronously and durably** on the
[job queue](./background-jobs) — an action cannot veto the event that triggered it.

## Implement an action

`IFlowActionHandler` (`src/Core/Application/Flows/Contracts/`):

```csharp
public interface IFlowActionHandler
{
    string Type { get; }  // action key, e.g. "call.accept" or "sms.send"
    Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default);
}
```

Two inputs, mirroring [conditions](./rules):

- **`RuleContext`** — the triggering event: `EventName`, `WorkspaceKey`, the flat `Data` bag, and
  `Now`. This is how the action reaches the thing that happened (the call id, the caller, …).
- **`parameters`** — the action step's `params`: the values the *operator* configured (a template
  id, a target, an audio file).

Unlike a condition, an action is **async and does real work** (and may throw — see below).

### How a built-in does it

`CallAcceptActionHandler` accepts the live inbound call from the triggering event. It extends a
small base that resolves the call from the event's `callId` field, so the concrete action stays
one line:

```csharp
public sealed class CallAcceptActionHandler(VoipCallHub callHub) : VoipCallFlowActionHandlerBase(callHub)
{
    public override string Type => "call.accept";

    protected override Task ExecuteOnCallAsync(
        ICall call, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken) =>
        call.AcceptAsync(cancellationToken);
}
```

The base (`VoipCallFlowActionHandlerBase`) shows the standard shape of pulling a resource out of
`context.Data` and failing loudly when the flow is misconfigured:

```csharp
public Task ExecuteAsync(
    RuleContext context, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
{
    if (!context.Data.TryGetValue("callId", out var callId) ||
        string.IsNullOrWhiteSpace(context.WorkspaceKey) ||
        !callHub.TryGet(context.WorkspaceKey, callId, out var call) || call is null)
    {
        throw new InvalidOperationException($"Flow action '{Type}' requires a live call; …");
    }

    return ExecuteOnCallAsync(call, parameters, cancellationToken);
}
```

::: warning Throwing surfaces as a failed job
`FlowExecuteJobHandler` runs actions in order and does not swallow exceptions — a throw fails the
`flow.execute` job, which lands in [`/api/jobs`](./background-jobs#monitoring). Because these
flow jobs are enqueued with `MaxAttempts: 1`, a failed action does **not** silently replay. An
**unknown** action type also throws: `"Flow '…' references unknown action type '…'"`. Your `Type`
string is the contract operators wire against — keep it stable.
:::

::: tip Design actions to be idempotent anyway
Flow-execution jobs default to a single attempt, so most flows won't replay. But the queue's
[at-least-once contract](./background-jobs#the-idempotency-contract) means an action *can* run more
than once (e.g. if you raise `MaxAttempts`, or a reclaim occurs). If your action has an external
effect — sending, charging, provisioning — guard it with a natural key so a second run is safe.
:::

## A worked "send an SMS" action

An action that sends an SMS to a target the operator configured, enriched from the event data:

```csharp
using Callora.Core.Application.Flows.Contracts;

public sealed class SendSmsActionHandler(ISmsGateway gateway) : IFlowActionHandler
{
    public string Type => "sms.send";

    public async Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        // "to" may be a literal number, or "{field}" to pull it from the event data.
        if (!parameters.TryGetValue("to", out var to) || string.IsNullOrWhiteSpace(to))
        {
            throw new InvalidOperationException("sms.send requires a 'to' parameter.");
        }
        if (to.StartsWith('{') && to.EndsWith('}'))
        {
            var field = to[1..^1];
            to = context.Data.TryGetValue(field, out var resolved)
                ? resolved
                : throw new InvalidOperationException($"sms.send: event has no field '{field}'.");
        }

        var body = parameters.TryGetValue("message", out var m) ? m : "You have a new notification.";

        // Idempotency key from the event so a replay of the same event doesn't double-send.
        var messageKey = $"{context.EventName}:{context.Data.GetValueOrDefault("callId") ?? to}";

        await gateway.SendAsync(context.WorkspaceKey, to, body, messageKey, cancellationToken);
    }
}
```

Export it from `StartAsync`:

```csharp
public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
{
    var gateway = context.Services.GetRequiredService<ISmsGateway>();
    context.Export<IFlowActionHandler>(new SendSmsActionHandler(gateway));
    return ValueTask.CompletedTask;
}
```

::: info Plugin actions can override host actions
The `FlowActionRegistry` resolves an action `Type` with **plugin exports winning over host
handlers** of the same key. Exporting an action whose `Type` matches a built-in replaces the
built-in for that key — deliberate, but easy to do by accident. Pick a distinct, namespaced key
(`myplugin.notify`) unless you *intend* to override.
:::

## How operators build flows

Operators compose your actions (and [conditions](./rules)) through the flow endpoints
(`src/Administration/Api/FlowEndpoints.cs`), under `/api/flows` and scoped to a `workspaceKey`:

| Method & route | Permission | Purpose |
| --- | --- | --- |
| `GET /api/flows?workspaceKey=…` | `flow.read` | List flows (paged) |
| `POST /api/flows?workspaceKey=…` | `flow.manage` | Create a flow |
| `PUT /api/flows/{id}?workspaceKey=…` | `flow.manage` | Update a flow |
| `DELETE /api/flows/{id}?workspaceKey=…` | `flow.manage` | Delete a flow |

Create and update take an `UpsertFlowApiRequest` (`src/Administration/Api/UpsertFlowApiRequest.cs`):

```csharp
public sealed record UpsertFlowApiRequest(
    string Name,
    string TriggerEvent,
    JsonElement? Conditions,   // the RuleConditionNode tree as JSON; null = match always
    JsonElement? Actions,      // the [{ "type", "params" }] array; null = "[]"
    bool IsActive = true,
    int Priority = 100);
```

`name` and `triggerEvent` are required (the endpoint rejects blanks with `400`). A worked create
that uses the `sms.send` action above, gated to unknown inbound callers:

```http
POST /api/flows?workspaceKey=acme
Content-Type: application/json

{
  "name": "Text me on missed calls from unknown numbers",
  "triggerEvent": "call.ringing",
  "conditions": {
    "type": "data.field",
    "params": { "field": "direction", "value": "inbound" }
  },
  "actions": [
    { "type": "sms.send", "params": { "to": "+491700000000", "message": "Incoming call from {callerNumber}" } }
  ],
  "priority": 50,
  "isActive": true
}
```

The response is `201 Created` with the persisted `FlowSnapshot` (including its new `Id`). From
now on, every `call.ringing` event in workspace `acme` whose `direction` field is `inbound`
enqueues a `flow.execute` job that runs your `sms.send` action.

## Next steps

- [Rules](./rules) — the **conditions** side of the same model
- [Background jobs](./background-jobs) — flow execution is a `flow.execute` job; the idempotency
  contract applies to your actions
- [Webhooks](./webhooks) — for pushing to external systems, prefer a standing subscription over
  an ad-hoc `webhook.send` action
- [Events & jobs](/guides/events-and-jobs) — the events that trigger flows
- [REST API reference](/reference/rest-api) — `/api/flows`
