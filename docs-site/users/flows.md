# Flows

Flows are Callora's low-code automation: "when this event happens, if these
conditions hold, do these things." They are workspace-scoped and driven by the
platform's business-event bus. The headline use case is **call routing** with the
Communication plugin.

## Rules and Flows

A **Flow** is an automation bound to one business event. It has:

- a **name**;
- a **trigger event** — the business event name it listens for (for example
  `call.ringing` or `workspace.created`);
- **conditions** — a **Rule**: a JSON tree of boolean logic that gates whether
  the flow runs (no conditions means "always");
- **actions** — an ordered JSON list of steps to run when the conditions pass;
- an **active** flag and a **priority** (lower runs first when several flows match
  the same event).

The **Rule** is the condition tree. Nodes combine with `and` / `or` / `not`, and
leaf nodes test facts about the event — for example an event-name match, a
data-field check, a time window, or a workspace-key match. Unknown leaf types
evaluate to `false` (and are logged), so a typo fails safe rather than matching
everything.

### How a flow runs

1. Something publishes a business event (a call comes in, a workspace is created,
   and so on).
2. Callora matches active flows for that trigger event and workspace.
3. For each match, the Rule is evaluated against the event's context.
4. If it passes, the flow's actions are enqueued as a durable background job and
   executed in order.

Because actions run as background jobs, they are retried and observable like any
other job — see [Operations](operations.md).

## Actions

Actions are looked up by type; the host provides some, and plugins contribute
more (a plugin's action overrides a host action of the same type). The action
types available to you therefore depend on which plugins are installed.

The **Communication** plugin contributes the call-control actions that make call
routing work:

- `call.accept` — answer the ringing inbound call the event names
- `call.reject` — turn it away instead
- `call.hangup` — end the call the event names, in whatever state it is
- `call.dtmf` — send keypad tones on it (`tones` parameter, e.g. `"123#"`)

Each action reads the call from the triggering event's `callId` field and runs
through the same call-control primitive the Admin API and MCP tools use, so a
rule that answers a call produces exactly the record an operator's click would
have. An action that finds no live call fails loudly rather than passing
silently — a call that ended between the trigger and the action is the normal
race, and a flow author should see it.

Playing audio into a call is not implemented; there is no media library to play
from yet.

## Call-routing example

A flow that auto-answers calls at night:

```json
{
  "name": "Auto-answer calls at night",
  "triggerEvent": "call.ringing",
  "conditions": {
    "type": "and",
    "children": [
      { "type": "time-window", "params": { "startHour": "22", "endHour": "06" } }
    ]
  },
  "actions": [
    { "type": "call.accept", "params": {} }
  ],
  "priority": 10
}
```

The condition names above illustrate the shape; the exact leaf types available
depend on the host and installed plugin versions.

## Where flows are managed

Flows live under `/flows` (Flows) in the admin shell (API base `/api/flows`,
always workspace-scoped):

| Action | Endpoint |
|---|---|
| List | `GET /api/flows?workspaceKey=...` |
| Create | `POST /api/flows?workspaceKey=...` |
| Update | `PUT /api/flows/{id}?workspaceKey=...` |
| Delete | `DELETE /api/flows/{id}?workspaceKey=...` |

Managing flows requires the `flow.manage` permission; reading them requires
`flow.read`.

> **Status:** The Flows screen manages a flow's name, trigger, conditions,
> actions, active flag, and priority. Conditions and actions are edited as JSON
> structures; a visual (drag-and-drop) rule/flow builder is not part of the
> current shell.

Next: [Operations](operations.md).
