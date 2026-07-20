# Rules

Rules and flows are Callora's **low-code automation**: an operator says *"when this event fires
and these conditions hold, run these actions"* — no code, just configuration. Your plugin's job
is to supply the vocabulary the operator composes from. A **rule condition** is half of that
vocabulary (the *when*); a [flow action](./flows) is the other half (the *do*).

This page covers conditions. You implement an `IRuleConditionEvaluator`, export it, and from then
on operators can drop your condition into any flow's condition tree via `/api/flows`.

The worked references are the built-in evaluators
(`src/Core/Application/Flows/Conditions/`): `DataFieldConditionEvaluator` (`data.field`) and
`TimeWindowConditionEvaluator` (`time.window`).

## What you'll learn

- What a rule condition is and how it fits the rule/flow model
- How to implement and export an `IRuleConditionEvaluator`
- The evaluation context (`RuleContext`) and the parameter bag operators fill in
- How the host combines your leaf condition with `and`/`or`/`not` into a tree
- A worked custom condition, and how an operator references it

## The rule model

A flow carries a **condition tree** as JSON (`FlowDefinition.ConditionsJson`). The tree is built
from `RuleConditionNode`s (`src/Core/Application/Flows/`):

```csharp
public sealed record RuleConditionNode(
    string Type,                        // "and" / "or" / "not", or a leaf type like "data.field"
    RuleConditionNode[]? Children = null,
    Dictionary<string, string>? Params = null);
```

`and`/`or`/`not` are **combinators** the host evaluates itself. Every other `Type` is a **leaf**,
resolved to an `IRuleConditionEvaluator` by its `Type` key. The `RuleEvaluator`
(`src/Core/Application/Flows/RuleEvaluator.cs`) walks the tree:

- `and` → all children must be true
- `or` → any child true
- `not` → negates its single child
- anything else → looked up in the evaluator registry and evaluated as a leaf

::: info Unknown conditions fail closed
If a leaf `Type` has no registered evaluator, `RuleEvaluator` logs a warning and evaluates it to
**`false`** — never silently `true`. A typo in a condition type makes the flow *not* match, rather
than matching everything. Your evaluator's `Type` string is the contract operators depend on.
:::

## Implement a condition

`IRuleConditionEvaluator` (`src/Core/Application/Flows/Contracts/`) is deliberately tiny:

```csharp
public interface IRuleConditionEvaluator
{
    string Type { get; }  // the leaf key, e.g. "call.direction" or "time.window"
    bool Evaluate(RuleContext context, IReadOnlyDictionary<string, string> parameters);
}
```

`Evaluate` is **synchronous and pure** — return `true` or `false`, no side effects. It receives
two things:

- **`RuleContext`** — the event that triggered the check.
- **`parameters`** — the leaf node's `Params`: the values the *operator* configured for this
  condition in this flow.

The `RuleContext` (`src/Core/Application/Flows/Contracts/RuleContext.cs`):

```csharp
public sealed record RuleContext(
    string EventName,                            // the triggering event, e.g. "call.ringing"
    string? WorkspaceKey,                        // null = host-wide
    IReadOnlyDictionary<string, string> Data,    // flat bag of event fields (from ToEventData())
    DateTimeOffset Now);                          // injected — use this, not DateTimeOffset.UtcNow
```

::: tip Read time from `Now`, never from the clock directly
`RuleContext.Now` is injected precisely so time-based conditions are **testable**. The built-in
`TimeWindowConditionEvaluator` reads `context.Now`; a unit test can then evaluate "is it inside
business hours?" for any fixed instant. Reaching for `DateTimeOffset.UtcNow` inside `Evaluate`
breaks that.
:::

### How built-ins do it

`DataFieldConditionEvaluator` matches one event field against a pattern — one class serving
`call.direction`, `call.state`, `call.target`, and every other field, instead of a class per field:

```csharp
public sealed class DataFieldConditionEvaluator : IRuleConditionEvaluator
{
    public string Type => "data.field";

    public bool Evaluate(RuleContext context, IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("field", out var field) ||
            !parameters.TryGetValue("value", out var pattern) ||
            string.IsNullOrWhiteSpace(field))
        {
            return false; // misconfigured leaf → does not match
        }

        if (!context.Data.TryGetValue(field.Trim(), out var actual))
        {
            return false; // event doesn't carry that field
        }

        var p = pattern?.Trim() ?? string.Empty;
        return p.EndsWith('*')
            ? actual.StartsWith(p[..^1], StringComparison.OrdinalIgnoreCase) // prefix match
            : string.Equals(actual, p, StringComparison.OrdinalIgnoreCase);  // exact match
    }
}
```

Note the pattern: **missing or malformed parameters return `false`**, never throw. A leaf that
threw would break the whole flow dispatch; returning `false` degrades gracefully.

## A worked custom condition

Suppose a plugin adds a `caller.vip` condition that matches when the event's caller is on a
maintained VIP list for the workspace. It reads a field from the event data and consults a store:

```csharp
using Callora.Core.Application.Flows.Contracts;

public sealed class VipCallerConditionEvaluator(IVipDirectory vipDirectory) : IRuleConditionEvaluator
{
    public string Type => "caller.vip";

    public bool Evaluate(RuleContext context, IReadOnlyDictionary<string, string> parameters)
    {
        // The operator picks which event field holds the caller (defaulting to "callerNumber").
        var field = parameters.TryGetValue("field", out var f) && !string.IsNullOrWhiteSpace(f)
            ? f.Trim()
            : "callerNumber";

        if (context.WorkspaceKey is null ||
            !context.Data.TryGetValue(field, out var caller) ||
            string.IsNullOrWhiteSpace(caller))
        {
            return false;
        }

        // Evaluation is synchronous, so use a cached/synchronous lookup here.
        return vipDirectory.IsVip(context.WorkspaceKey, caller);
    }
}
```

Export it from `StartAsync`:

```csharp
public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
{
    var vipDirectory = context.Services.GetRequiredService<IVipDirectory>();
    context.Export<IRuleConditionEvaluator>(new VipCallerConditionEvaluator(vipDirectory));
    return ValueTask.CompletedTask;
}
```

::: warning `Evaluate` is synchronous
The interface is `bool Evaluate(...)`, not `Task<bool>`. Do fast, in-memory or cached lookups.
The evaluator runs for every matching flow on every triggering event, so a blocking database
call per condition is a hot-path hazard — prime a cache elsewhere and read it here.
:::

## How an operator uses it

Once exported, the condition type is available in any flow's condition tree. An operator creates a
flow via `POST /api/flows` (see [Flows](./flows)) whose `conditions` reference your `Type`. To
match a VIP caller during business hours:

```json
{
  "name": "Priority routing for VIP daytime calls",
  "triggerEvent": "call.ringing",
  "conditions": {
    "type": "and",
    "children": [
      { "type": "caller.vip", "params": { "field": "callerNumber" } },
      { "type": "time.window", "params": { "days": "mon,tue,wed,thu,fri", "from": "09:00", "to": "17:00", "timezone": "Europe/Berlin" } }
    ]
  },
  "actions": [ { "type": "call.accept", "params": {} } ]
}
```

When a `call.ringing` event fires, the host builds a `RuleContext` from the event, walks this tree
— your `caller.vip` leaf *and* the built-in `time.window` leaf — and, if it matches, enqueues the
`actions` as a flow-execution job. Your job was only to make `caller.vip` mean something; the
operator composed the rest.

## Next steps

- [Flows](./flows) — contribute the **actions** that run when a rule matches, and the full
  `/api/flows` operator surface
- [Background jobs](./background-jobs) — how a matched flow's actions are executed durably
- [Events & jobs](/guides/events-and-jobs) — the business events that trigger flows and populate
  `RuleContext.Data`
- [Exporting extensions](/guides/fundamentals/exporting-extensions) — exporting your evaluator
