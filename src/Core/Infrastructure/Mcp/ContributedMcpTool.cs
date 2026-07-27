using System.Security.Claims;
using System.Text.Json;
using Callora.Core.Application.Mcp.Contracts;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Callora.Core.Infrastructure.Mcp;

/// <summary>
/// Adapts one SDK-neutral <see cref="McpToolRegistration"/> onto the MCP SDK's <see cref="McpServerTool"/>.
/// It advertises the plugin's explicit input schema and, on each call, authenticates via the ambient
/// <see cref="HttpContext"/> principal, resolves the workspace and enforces the tool's required
/// permission before handing an already-scoped <see cref="McpToolInvocation"/> to the plugin handler.
/// Every failure is returned as an error <see cref="CallToolResult"/>; no exception escapes to the
/// transport.
/// </summary>
internal sealed class ContributedMcpTool : McpServerTool
{
    private readonly McpToolRegistration _registration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Tool _protocolTool;

    public ContributedMcpTool(McpToolRegistration registration, IHttpContextAccessor httpContextAccessor)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _protocolTool = new Tool
        {
            Name = registration.Name,
            Description = registration.Description,
            InputSchema = registration.InputSchema
        };
    }

    /// <inheritdoc />
    public override Tool ProtocolTool => _protocolTool;

    /// <inheritdoc />
    public override IReadOnlyList<object> Metadata => [];

    /// <inheritdoc />
    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = _httpContextAccessor.HttpContext?.User;
        var arguments = ReadArguments(request.Params?.Arguments);
        return InvokeCoreAsync(user, arguments, cancellationToken);
    }

    // The authorize→scope→invoke→map flow, isolated from the transport's RequestContext so it is
    // directly testable with a principal and raw arguments.
    internal async ValueTask<CallToolResult> InvokeCoreAsync(
        ClaimsPrincipal? user,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Error("The tool call is not authenticated.");
        }

        if (!McpToolAuthorization.HasPermission(user, _registration.RequiredPermission))
        {
            return Error($"Permission '{_registration.RequiredPermission}' is required.");
        }

        if (!McpToolAuthorization.TryResolveWorkspace(user, arguments, out var workspaceKey, out var scopeError))
        {
            return ToCallToolResult(scopeError!);
        }

        McpToolResult result;
        try
        {
            result = await _registration
                .Handler(new McpToolInvocation(arguments, workspaceKey), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A plugin handler must never surface a raw exception to the MCP client; report it as a
            // tool-level error instead.
            return Error(ex.Message);
        }

        return ToCallToolResult(result);
    }

    private static JsonElement ReadArguments(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return JsonDocument.Parse("{}").RootElement;
        }

        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in arguments)
            {
                writer.WritePropertyName(key);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(buffer.WrittenMemory).RootElement;
    }

    private static CallToolResult Error(string message) =>
        ToCallToolResult(McpToolResult.Error(message));

    private static CallToolResult ToCallToolResult(McpToolResult result) =>
        new()
        {
            IsError = result.IsError,
            Content = [new TextContentBlock { Text = result.Content }]
        };
}
