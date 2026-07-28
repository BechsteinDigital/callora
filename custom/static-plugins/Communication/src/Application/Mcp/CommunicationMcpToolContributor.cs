using System.Text.Json;
using Callora.Core.Application.Mcp.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Admin.Calls;

namespace Callora.Plugin.Communication.Application.Mcp;

/// <summary>
/// Contributes the Communication plugin's call-control primitives as MCP tools (<c>place_call</c>,
/// <c>hangup_call</c>, <c>get_call</c>, <c>list_recent_calls</c>) over the neutral
/// <see cref="IMcpToolContributor"/> contract. The plugin only supplies the "content": each tool names
/// its RBAC permission and JSON-Schema, and delegates to <see cref="ICallControlService"/>. Transport,
/// authentication, workspace scope and permission enforcement all live in the host — the handlers here
/// receive an <em>already-scoped</em> <see cref="McpToolInvocation"/> and never re-derive the workspace.
/// This is the out-of-process AI-agent face of the same primitive in-process plugins consume via DI and
/// operators reach over REST.
/// </summary>
public sealed class CommunicationMcpToolContributor : IMcpToolContributor
{
    // Guards against runaway history reads; mirrors the REST list cap so the shape is identical.
    private const int DefaultRecentLimit = 50;
    private const int MaxRecentLimit = 200;

    private readonly ICallControlService _callControl;

    /// <summary>Creates the contributor over the workspace's call-control primitive.</summary>
    public CommunicationMcpToolContributor(ICallControlService callControl)
    {
        ArgumentNullException.ThrowIfNull(callControl);
        _callControl = callControl;
    }

    /// <inheritdoc />
    public IReadOnlyList<McpToolRegistration> Tools =>
    [
        new McpToolRegistration(
            "place_call",
            "Places one outbound call from the workspace's voice channel to a target address.",
            PlaceCallSchema,
            CommunicationPermissionKeys.CallsManage,
            PlaceCallAsync),
        new McpToolRegistration(
            "hangup_call",
            "Ends a live call owned by the workspace.",
            HangupCallSchema,
            CommunicationPermissionKeys.CallsManage,
            HangupCallAsync),
        new McpToolRegistration(
            "get_call",
            "Returns a snapshot of a live call owned by the workspace, or a not-found marker.",
            GetCallSchema,
            CommunicationPermissionKeys.CallsRead,
            GetCall),
        new McpToolRegistration(
            "list_recent_calls",
            "Lists the workspace's most recent recorded calls, newest first.",
            ListRecentCallsSchema,
            CommunicationPermissionKeys.CallsRead,
            ListRecentCallsAsync),
    ];

    // place_call: {to (required), channelId?, displayName?} → CallView JSON. A missing voice channel is
    // a tool-level error (mapped by the host), never an exception across the transport boundary.
    private async Task<McpToolResult> PlaceCallAsync(McpToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (!TryGetRequiredString(invocation.Arguments, "to", out var to))
        {
            return McpToolResult.Error("'to' is required and must be a non-empty string.");
        }

        var channelId = GetOptionalString(invocation.Arguments, "channelId");
        var displayName = GetOptionalString(invocation.Arguments, "displayName");

        try
        {
            var snapshot = await _callControl
                .PlaceCallAsync(new PlaceCallCommand(invocation.WorkspaceKey, to, channelId, displayName), cancellationToken)
                .ConfigureAwait(false);
            return McpToolResult.Json(CallView.From(snapshot));
        }
        catch (InvalidOperationException ex)
        {
            return McpToolResult.Error(ex.Message);
        }
    }

    // hangup_call: {callId (required)} → { hungUp: bool } reflecting whether a live call was ended.
    private async Task<McpToolResult> HangupCallAsync(McpToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (!TryGetRequiredString(invocation.Arguments, "callId", out var callId))
        {
            return McpToolResult.Error("'callId' is required and must be a non-empty string.");
        }

        var hungUp = await _callControl
            .HangupAsync(invocation.WorkspaceKey, callId, cancellationToken)
            .ConfigureAwait(false);
        return McpToolResult.Json(new { hungUp });
    }

    // get_call: {callId (required)} → CallView, or { found: false } when the call is not tracked.
    private Task<McpToolResult> GetCall(McpToolInvocation invocation, CancellationToken cancellationToken)
    {
        if (!TryGetRequiredString(invocation.Arguments, "callId", out var callId))
        {
            return Task.FromResult(McpToolResult.Error("'callId' is required and must be a non-empty string."));
        }

        var snapshot = _callControl.Get(invocation.WorkspaceKey, callId);
        var result = snapshot is null
            ? McpToolResult.Json(new { found = false })
            : McpToolResult.Json(CallView.From(snapshot));
        return Task.FromResult(result);
    }

    // list_recent_calls: {limit?} → CallHistoryEntry[] (already string-enum JSON), default 50, hard cap 200.
    private async Task<McpToolResult> ListRecentCallsAsync(McpToolInvocation invocation, CancellationToken cancellationToken)
    {
        var limit = ResolveLimit(GetOptionalInt(invocation.Arguments, "limit"));
        var entries = await _callControl
            .ListRecentAsync(invocation.WorkspaceKey, limit, cancellationToken)
            .ConfigureAwait(false);
        return McpToolResult.Json(entries);
    }

    // Clamps the requested limit into [1, 200]; an absent/non-positive request falls back to the default.
    private static int ResolveLimit(int? requested)
    {
        if (requested is not { } value || value <= 0)
        {
            return DefaultRecentLimit;
        }

        return Math.Min(value, MaxRecentLimit);
    }

    // Reads a required string argument; treats missing, wrong-typed and whitespace-only values as absent.
    private static bool TryGetRequiredString(JsonElement arguments, string name, out string value)
    {
        var candidate = GetOptionalString(arguments, name);
        if (candidate is null)
        {
            value = string.Empty;
            return false;
        }

        value = candidate;
        return true;
    }

    // Reads an optional string argument, or null when missing/empty/not a string.
    private static string? GetOptionalString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = property.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    // Reads an optional integer argument, or null when missing/not an integral number.
    private static int? GetOptionalInt(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(name, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out var value))
        {
            return null;
        }

        return value;
    }

    // --- JSON-Schema input contracts (built once as immutable JsonElement objects) ---

    private static readonly JsonElement PlaceCallSchema = SerializeSchema(new
    {
        type = "object",
        properties = new
        {
            to = new { type = "string", description = "Channel-neutral target address, e.g. \"+49301234567\"." },
            channelId = new { type = "string", description = "Optional explicit channel; omit to use the first voice-capable channel." },
            displayName = new { type = "string", description = "Optional human-readable name for the remote party." },
            workspaceKey = WorkspaceKeyProperty,
        },
        required = new[] { "to" },
    });

    private static readonly JsonElement HangupCallSchema = SerializeSchema(new
    {
        type = "object",
        properties = new
        {
            callId = new { type = "string", description = "Identifier of the live call to end." },
            workspaceKey = WorkspaceKeyProperty,
        },
        required = new[] { "callId" },
    });

    private static readonly JsonElement GetCallSchema = SerializeSchema(new
    {
        type = "object",
        properties = new
        {
            callId = new { type = "string", description = "Identifier of the call to look up." },
            workspaceKey = WorkspaceKeyProperty,
        },
        required = new[] { "callId" },
    });

    private static readonly JsonElement ListRecentCallsSchema = SerializeSchema(new
    {
        type = "object",
        properties = new
        {
            limit = new { type = "integer", description = "Maximum entries to return (default 50, capped at 200)." },
            workspaceKey = WorkspaceKeyProperty,
        },
    });

    // The workspaceKey argument is only meaningful for platform operators (token-bound callers have it
    // resolved from their claim). It is advertised so operators can send it; the host — not the handler —
    // reads it. Declared once and shared across every tool schema.
    private static object WorkspaceKeyProperty => new
    {
        type = "string",
        description = "Target workspace; only platform operators need to supply it (resolved from the token otherwise).",
    };

    private static JsonElement SerializeSchema(object schema) => JsonSerializer.SerializeToElement(schema);
}
