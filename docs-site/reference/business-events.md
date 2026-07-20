# Business events

Business events are the platform's generic notification mechanism: a subsystem or
plugin publishes a named event onto `IBusinessEventBus`, and the host fans it out to
every flow, webhook subscription, and plugin listener that cares. This page
catalogues the events the host publishes, their payload fields, and how a plugin
subscribes.

> **Status:** The set of business events is **not centrally enumerated** in a single
> registry constant — event names live next to the code that publishes them (in
> `…/Events/*EventTypes.cs` classes) and are surfaced for discovery through
> `IBusinessEventProvider` descriptors. The catalogue below is **exhaustive for the
> host events verified from real publish sites** as of this writing; plugins can add
> their own events, so the full runtime set on any given install may be larger.

## Anatomy of a business event

Every business event implements `IBusinessEvent`
(`Callora.Core.Application.Events.Contracts`):

| Member | Meaning |
| --- | --- |
| `string EventName` | Stable dotted name consumers subscribe by (e.g. `workspace.created`). |
| `string? WorkspaceKey` | Workspace the event belongs to; `null` for platform-wide events. Scopes flow/webhook matching. |
| `IReadOnlyDictionary<string,string> ToEventData()` | Flat string projection of the payload — the fields a flow condition, webhook payload, or mail template can reference. |

### Notification vs. mutable (cancelable) events

- A plain `IBusinessEvent` is a **post-hoc notification** — it fans out *after* the
  operation committed. Publishing is best-effort; a failing listener is logged and
  swallowed (`BusinessEventBus` isolates listener failures).
- A `MutableBusinessEvent` (base in `Callora.Core.Application.Events`) is a **"before"
  event** that lets a listener intervene *before* the operation commits. Through the
  shared `InterceptableEvent` base it can share data via `State`, skip remaining
  listeners via `StopPropagation()`, and veto via `Cancel()`. The publisher inspects
  `IsCanceled` after `PublishAsync` and aborts the operation when set. By convention
  these carry an `-ing` name (e.g. `call.starting`), paired with a read-only `-ed`
  event on completion.

> **Status:** `MutableBusinessEvent` is a shipped, documented base class, but **no
> concrete `MutableBusinessEvent` subclass is published by the host today** — every
> host event in the catalogue below is a plain (read-only) notification. The mutable
> base is an extension point available to plugins and future host events. The dotted
> names `call.ringing` / `call.starting` appear only as **illustrative examples** in
> the contract XML docs and in flow/webhook field comments; they are **not** published
> anywhere in the current codebase.

## The host event catalogue

Grouped by area. Each event's fields are the keys returned by `ToEventData()` (and
declared for discovery in the matching `IBusinessEventProvider`). All host events
below are read-only notifications.

### Workspace lifecycle

Names in `WorkspaceEventTypes`. Published from `WorkspaceEndpoints` after the mutation
commits (best-effort via `PublishSafelyAsync`).

| Event name | Published when | Payload fields |
| --- | --- | --- |
| `workspace.created` | A workspace is created (`WorkspaceBusinessEvent.ForUpsert` — created vs. updated is told apart by matching created/updated timestamps). | `workspaceKey`, `tenantKey`, `displayName`, `workspaceType`, `isActive` |
| `workspace.updated` | An existing workspace is updated. | same as above |
| `workspace.deleted` | A workspace is deleted and its data purged (`ForDeletion`). | same as above |

### Workspace membership

Names in `WorkspaceMemberEventTypes`. Published from `WorkspaceEndpoints` on member
assignment/removal.

| Event name | Published when | Payload fields |
| --- | --- | --- |
| `workspace.member-assigned` | A member is added to a workspace or has its role changed. | `workspaceKey`, `userId`, `role`, `email`, `displayName` |
| `workspace.member-removed` | A member is removed from a workspace. | `workspaceKey`, `userId` |

### User accounts (platform-wide)

Names in `UserEventTypes`. Published from `UserEndpoints`. Users are platform-wide, so
`WorkspaceKey` is `null`.

| Event name | Published when | Payload fields |
| --- | --- | --- |
| `user.created` | A user account is created. | `userId`, `email`, `displayName` |
| `user.updated` | A user account is updated. | `userId`, `email`, `displayName` |
| `user.deleted` | A user account is deleted (audit trail anonymized). | `userId` |

### Media assets

Names in `MediaEventTypes`. Published from `MediaEndpoints`.

| Event name | Published when | Payload fields |
| --- | --- | --- |
| `media.uploaded` | A media asset is uploaded to a workspace. | `mediaId`, `workspaceKey`, `fileName`, `contentType`, `folder`, `sizeBytes` |
| `media.deleted` | A media asset is deleted from a workspace. | same as above |

::: info Field types
Discovery descriptors type each field via `BusinessEventFieldType`
(`Text`, `Number`, `Boolean`, `Timestamp`) so the flow-builder and webhook UI can
render appropriate inputs — e.g. `isActive` is `Boolean`, `sizeBytes` is `Number`.
The runtime `ToEventData()` payload is always string-projected.
:::

## Plugin lifecycle event (separate channel)

`PluginLifecycleChangedEvent` is **not** a business event — it is an
`IHostApplicationEvent` on the host application-event channel, published by
`PluginLifecycleReporter` for install/activate/deactivate/uninstall reporting. It
carries `OccurredAtUtc`, `Action`, `PluginId`, `IsSuccess`, `RequestedBy`, `Message`,
and optional `Metadata`. Subscribe to it via the host application-event
subscriber interface rather than `IBusinessEventListener`.

## How a plugin subscribes

Implement `IBusinessEventListener` and export it from your plugin. Every listener
receives **every** published business event, so filter on `EventName` yourself.

```csharp
public sealed class WorkspaceProvisioningListener : IBusinessEventListener
{
    // Higher priority runs earlier; host rails (flow trigger, webhook relay) use 0.
    public int Priority => 10;

    public async Task OnBusinessEventAsync(
        IBusinessEvent businessEvent,
        CancellationToken cancellationToken = default)
    {
        if (businessEvent.EventName != "workspace.created")
        {
            return;
        }

        var data = businessEvent.ToEventData();
        var workspaceKey = data["workspaceKey"];
        // ... react (provision resources, call an external system, etc.)
    }
}
```

Export the listener through your plugin context so the host merges it with its own
rails. The bus resolves host listeners from DI plus plugin-exported listeners, orders
them by `Priority` (descending), and isolates failures so one bad listener does not
stop the others.

### Built-in host listeners

Two host rails subscribe to every business event (both at `Priority = 0`):

- **`FlowBusinessEventListener`** — matches active flows by trigger name +
  conditions and enqueues one durable `flow.execute` job per match.
- **`WebhookBusinessEventListener`** — dispatches matching webhook subscriptions with
  the (minimized) event data. A subscription's event filter may be `*` to match all
  events.

## Discovering events at runtime

`BusinessEventRegistry.ListDescriptors()` aggregates every event descriptor from host
providers (`IBusinessEventProvider`) and plugin exports — this powers the flow-builder
and webhook UI, and is exposed at `GET /api/events/catalog` (permission: `flow.read`).
A plugin makes its own events discoverable by exporting an `IBusinessEventProvider`
returning `BusinessEventDescriptor`s (name, display name, fields).

## See also

- [Events and jobs](/guides/events-and-jobs) — subscribing, flows, and the job queue.
- [Backend extensions](/guides/backend-extensions) — exporting listeners and providers.
- [REST API](rest-api.md) — the `/api/events/catalog`, `/api/flows`, and
  `/api/webhooks` endpoints.
