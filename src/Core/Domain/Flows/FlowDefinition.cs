namespace Callora.Core.Domain.Flows;

/// <summary>
/// One low-code automation: when the trigger event fires and the condition
/// tree matches, the action list executes sequentially as durable job.
/// </summary>
public sealed class FlowDefinition
{
    public Guid Id { get; set; }

    public string WorkspaceKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Trigger event name, e.g. "call.ringing".</summary>
    public string TriggerEvent { get; set; } = string.Empty;

    /// <summary>JSON condition tree (RuleConditionNode); null matches always.</summary>
    public string? ConditionsJson { get; set; }

    /// <summary>JSON array of actions: [{ "type": "...", "params": { ... } }].</summary>
    public string ActionsJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    /// <summary>Lower runs first when multiple flows match.</summary>
    public int Priority { get; set; } = 100;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
