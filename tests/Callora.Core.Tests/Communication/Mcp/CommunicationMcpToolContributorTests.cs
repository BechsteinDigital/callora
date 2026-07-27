using System.Text.Json;
using Callora.Core.Application.Mcp.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Application.Mcp;
using Xunit;

namespace Callora.Core.Tests.Communication.Mcp;

/// <summary>
/// The Communication MCP tool contributor (M2): four call-control tools over <see cref="ICallControlService"/>.
/// It exposes only the "content" — argument parsing, delegation to the service and neutral JSON results —
/// while the host owns transport, auth, workspace scope and permission enforcement. These tests drive the
/// registration handlers directly with an already-scoped <see cref="McpToolInvocation"/> and a fake service,
/// asserting: the resolved workspace is passed verbatim, string-enum JSON is produced, missing required
/// arguments error without touching the service, and each tool advertises the correct RBAC permission.
/// </summary>
public sealed class CommunicationMcpToolContributorTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public async Task PlaceCall_PassesArgumentsAndResolvedWorkspace_AndReturnsStringEnumCallView()
    {
        var fake = new FakeCallControlService
        {
            NextSnapshot = new CallSnapshot("call-1", CallDirection.Outbound, CallState.Connecting, "+49301234567"),
        };
        var handler = HandlerFor(fake, "place_call");

        var result = await handler(
            Invocation("""{"to":"+49301234567","channelId":"line-1","displayName":"Bob"}"""),
            CancellationToken.None);

        Assert.False(result.IsError);
        var placed = Assert.Single(fake.PlaceCalls);
        Assert.Equal(new PlaceCallCommand(Workspace, "+49301234567", "line-1", "Bob"), placed);

        using var json = JsonDocument.Parse(result.Content);
        Assert.Equal("call-1", json.RootElement.GetProperty("CallId").GetString());
        // String enums, not numbers.
        Assert.Equal("Outbound", json.RootElement.GetProperty("Direction").GetString());
        Assert.Equal("Connecting", json.RootElement.GetProperty("State").GetString());
        Assert.Equal("+49301234567", json.RootElement.GetProperty("Target").GetString());
    }

    [Fact]
    public async Task PlaceCall_WhenServiceThrowsInvalidOperation_IsErrorWithoutThrowing()
    {
        var fake = new FakeCallControlService { PlaceCallError = new InvalidOperationException("no voice channel") };
        var handler = HandlerFor(fake, "place_call");

        var result = await handler(Invocation("""{"to":"+49301234567"}"""), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("no voice channel", result.Content);
    }

    [Fact]
    public async Task PlaceCall_WithoutRequiredTo_IsError_AndServiceNotCalled()
    {
        var fake = new FakeCallControlService();
        var handler = HandlerFor(fake, "place_call");

        var result = await handler(Invocation("""{"channelId":"line-1"}"""), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(fake.PlaceCalls);
    }

    [Fact]
    public async Task PlaceCall_WithWrongTypedTo_IsError_AndServiceNotCalled()
    {
        // A number where a string is required is treated as absent (wrong-type, not a coercion).
        var fake = new FakeCallControlService();
        var handler = HandlerFor(fake, "place_call");

        var result = await handler(
            new McpToolInvocation(JsonSerializer.SerializeToElement(new { to = 42 }), Workspace),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(fake.PlaceCalls);
    }

    [Fact]
    public async Task Hangup_PassesCallIdAndWorkspace_AndReturnsHungUpTrue()
    {
        var fake = new FakeCallControlService { HangupResult = true };
        var handler = HandlerFor(fake, "hangup_call");

        var result = await handler(Invocation("""{"callId":"call-9"}"""), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal((Workspace, "call-9"), Assert.Single(fake.Hangups));
        using var json = JsonDocument.Parse(result.Content);
        Assert.True(json.RootElement.GetProperty("hungUp").GetBoolean());
    }

    [Fact]
    public async Task Hangup_WhenServiceReturnsFalse_ReturnsHungUpFalse()
    {
        var fake = new FakeCallControlService { HangupResult = false };
        var handler = HandlerFor(fake, "hangup_call");

        var result = await handler(Invocation("""{"callId":"call-9"}"""), CancellationToken.None);

        Assert.False(result.IsError);
        using var json = JsonDocument.Parse(result.Content);
        Assert.False(json.RootElement.GetProperty("hungUp").GetBoolean());
    }

    [Fact]
    public async Task Hangup_WithoutRequiredCallId_IsError_AndServiceNotCalled()
    {
        var fake = new FakeCallControlService();
        var handler = HandlerFor(fake, "hangup_call");

        var result = await handler(Invocation("{}"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(fake.Hangups);
    }

    [Fact]
    public async Task GetCall_WhenFound_ReturnsStringEnumCallView()
    {
        var fake = new FakeCallControlService
        {
            NextSnapshot = new CallSnapshot("call-1", CallDirection.Inbound, CallState.Ringing, "+49301112222"),
        };
        var handler = HandlerFor(fake, "get_call");

        var result = await handler(Invocation("""{"callId":"call-1"}"""), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal((Workspace, "call-1"), Assert.Single(fake.Gets));
        using var json = JsonDocument.Parse(result.Content);
        Assert.Equal("Inbound", json.RootElement.GetProperty("Direction").GetString());
        Assert.Equal("Ringing", json.RootElement.GetProperty("State").GetString());
    }

    [Fact]
    public async Task GetCall_WhenNotFound_ReturnsFoundFalse()
    {
        var fake = new FakeCallControlService { NextSnapshot = null };
        var handler = HandlerFor(fake, "get_call");

        var result = await handler(Invocation("""{"callId":"missing"}"""), CancellationToken.None);

        Assert.False(result.IsError);
        using var json = JsonDocument.Parse(result.Content);
        Assert.False(json.RootElement.GetProperty("found").GetBoolean());
    }

    [Fact]
    public async Task ListRecent_WithoutLimit_UsesDefaultOf50()
    {
        var fake = new FakeCallControlService();
        var handler = HandlerFor(fake, "list_recent_calls");

        var result = await handler(Invocation("{}"), CancellationToken.None);

        Assert.False(result.IsError);
        var (workspace, limit) = Assert.Single(fake.Lists);
        Assert.Equal(Workspace, workspace);
        Assert.Equal(50, limit);
    }

    [Fact]
    public async Task ListRecent_WithWrongTypedLimit_UsesDefaultOf50()
    {
        // A string where an integer is expected is treated as absent → the default limit applies.
        var fake = new FakeCallControlService();
        var handler = HandlerFor(fake, "list_recent_calls");

        var result = await handler(
            new McpToolInvocation(JsonSerializer.SerializeToElement(new { limit = "foo" }), Workspace),
            CancellationToken.None);

        Assert.False(result.IsError);
        var (_, limit) = Assert.Single(fake.Lists);
        Assert.Equal(50, limit);
    }

    [Fact]
    public async Task ListRecent_WithLimitAboveCap_ClampsTo200()
    {
        var fake = new FakeCallControlService();
        var handler = HandlerFor(fake, "list_recent_calls");

        await handler(Invocation("""{"limit":1000}"""), CancellationToken.None);

        var (_, limit) = Assert.Single(fake.Lists);
        Assert.Equal(200, limit);
    }

    [Fact]
    public async Task ListRecent_ReturnsHistoryEntriesAsJsonArray()
    {
        var fake = new FakeCallControlService
        {
            NextHistory =
            [
                new CallHistoryEntry("h-1", "Outbound", "+49301", DateTimeOffset.UnixEpoch, null, null, 0, "InProgress", null),
            ],
        };
        var handler = HandlerFor(fake, "list_recent_calls");

        var result = await handler(Invocation("""{"limit":10}"""), CancellationToken.None);

        Assert.False(result.IsError);
        using var json = JsonDocument.Parse(result.Content);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        var entry = Assert.Single(json.RootElement.EnumerateArray().ToArray());
        Assert.Equal("h-1", entry.GetProperty("CallId").GetString());
        Assert.Equal("Outbound", entry.GetProperty("Direction").GetString());
    }

    [Fact]
    public void WorkspaceKey_FromInvocation_IsPassedVerbatim_NotReParsedFromArguments()
    {
        // The host resolved the workspace; the handler must use invocation.WorkspaceKey, ignoring any
        // conflicting workspaceKey argument.
        var fake = new FakeCallControlService
        {
            NextSnapshot = new CallSnapshot("call-1", CallDirection.Outbound, CallState.Connecting, "+49301"),
        };
        var handler = HandlerFor(fake, "place_call");

        _ = handler(
            new McpToolInvocation(Args("""{"to":"+49301","workspaceKey":"ws-attacker"}"""), Workspace),
            CancellationToken.None);

        Assert.Equal(Workspace, Assert.Single(fake.PlaceCalls).WorkspaceKey);
    }

    [Fact]
    public void RequiredPermissions_MatchCommunicationPermissionKeys()
    {
        var tools = new CommunicationMcpToolContributor(new FakeCallControlService()).Tools;

        Assert.Equal(CommunicationPermissionKeys.CallsManage, Registration(tools, "place_call").RequiredPermission);
        Assert.Equal(CommunicationPermissionKeys.CallsManage, Registration(tools, "hangup_call").RequiredPermission);
        Assert.Equal(CommunicationPermissionKeys.CallsRead, Registration(tools, "get_call").RequiredPermission);
        Assert.Equal(CommunicationPermissionKeys.CallsRead, Registration(tools, "list_recent_calls").RequiredPermission);
    }

    [Fact]
    public void EveryTool_HasValidObjectInputSchema()
    {
        var tools = new CommunicationMcpToolContributor(new FakeCallControlService()).Tools;

        Assert.Equal(4, tools.Count);
        foreach (var tool in tools)
        {
            Assert.Equal(JsonValueKind.Object, tool.InputSchema.ValueKind);
            Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
            Assert.True(tool.InputSchema.TryGetProperty("properties", out _));
        }
    }

    private static Func<McpToolInvocation, CancellationToken, Task<McpToolResult>> HandlerFor(
        ICallControlService service, string toolName) =>
        Registration(new CommunicationMcpToolContributor(service).Tools, toolName).Handler;

    private static McpToolRegistration Registration(IReadOnlyList<McpToolRegistration> tools, string name) =>
        tools.Single(t => t.Name == name);

    private static McpToolInvocation Invocation(string argumentsJson) =>
        new(Args(argumentsJson), Workspace);

    private static JsonElement Args(string json) => JsonSerializer.SerializeToElement(JsonDocument.Parse(json).RootElement);

    /// <summary>Records call-control invocations and returns pre-seeded results.</summary>
    private sealed class FakeCallControlService : ICallControlService
    {
        public List<PlaceCallCommand> PlaceCalls { get; } = [];
        public List<(string Workspace, string CallId)> Hangups { get; } = [];
        public List<(string Workspace, string CallId)> Gets { get; } = [];
        public List<(string Workspace, int Limit)> Lists { get; } = [];

        public CallSnapshot? NextSnapshot { get; init; }
        public InvalidOperationException? PlaceCallError { get; init; }
        public bool HangupResult { get; init; }
        public IReadOnlyList<CallHistoryEntry> NextHistory { get; init; } = [];

        public Task<CallSnapshot> PlaceCallAsync(PlaceCallCommand command, CancellationToken cancellationToken = default)
        {
            PlaceCalls.Add(command);
            if (PlaceCallError is not null)
            {
                throw PlaceCallError;
            }

            return Task.FromResult(NextSnapshot!);
        }

        public Task<bool> HangupAsync(string workspaceKey, string callId, CancellationToken cancellationToken = default)
        {
            Hangups.Add((workspaceKey, callId));
            return Task.FromResult(HangupResult);
        }

        public CallSnapshot? Get(string workspaceKey, string callId)
        {
            Gets.Add((workspaceKey, callId));
            return NextSnapshot;
        }

        public Task<IReadOnlyList<CallHistoryEntry>> ListRecentAsync(string workspaceKey, int limit, CancellationToken cancellationToken = default)
        {
            Lists.Add((workspaceKey, limit));
            return Task.FromResult(NextHistory);
        }
    }
}
