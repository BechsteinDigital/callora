using Callora.Core.Application.Flows.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Flows;

/// <summary>
/// Base for the flow actions that operate on the call a <c>call.*</c> event names.
/// </summary>
/// <remarks>
/// Every action goes through <see cref="ICallControlService"/> rather than reaching for the provider's
/// call object (#116). That is what makes a flow subject to the same workspace ownership check, the
/// same state machine and the same history writes as the REST and MCP faces — a rule that hangs a call
/// up produces exactly the record an operator's click would have.
/// </remarks>
public abstract class CallFlowActionHandlerBase(ICallControlService callControl) : IFlowActionHandler
{
    /// <summary>Data field naming the call a <c>call.*</c> event refers to.</summary>
    protected const string CallIdField = "callId";

    /// <inheritdoc />
    public abstract string Type { get; }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);

        if (string.IsNullOrWhiteSpace(context.WorkspaceKey))
        {
            throw new InvalidOperationException(
                $"Flow action '{Type}' is workspace-scoped; the triggering event carries no workspace.");
        }

        if (!context.Data.TryGetValue(CallIdField, out var callId) || string.IsNullOrWhiteSpace(callId))
        {
            throw new InvalidOperationException(
                $"Flow action '{Type}' requires a '{CallIdField}' field; the triggering event carries none.");
        }

        // A call that ended between the trigger and the action is the normal race, not a fault: the
        // rule fired on a real event and the world moved on. It is reported so a flow author sees it,
        // and reported as this specific failure rather than a generic one.
        if (!await ExecuteOnCallAsync(context.WorkspaceKey, callId, parameters, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                $"Flow action '{Type}' found no live call '{callId}' in workspace '{context.WorkspaceKey}'.");
        }
    }

    /// <summary>The call-control operation this action performs. Returns whether a live call was found.</summary>
    protected abstract Task<bool> ExecuteOnCallAsync(
        string workspaceKey,
        string callId,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken);

    /// <summary>The call-control primitive every action delegates to.</summary>
    protected ICallControlService CallControl { get; } = callControl ?? throw new ArgumentNullException(nameof(callControl));
}
