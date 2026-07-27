using System.Security.Claims;
using System.Text.Json;
using Callora.Core.Application.Mcp.Contracts;
using Callora.Core.Application.Plugins;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Callora.Core.Infrastructure.Mcp;

/// <summary>
/// Adapts one SDK-neutral <see cref="McpToolRegistration"/> onto the MCP SDK's <see cref="McpServerTool"/>.
/// It advertises the plugin's explicit input schema and, on each call, authenticates via the ambient
/// <see cref="HttpContext"/> principal, resolves the workspace, enforces the tool's required permission
/// and then runs the contributing plugin through the internal availability gate
/// (<see cref="IPluginAvailabilityEvaluator"/>) before handing an already-scoped
/// <see cref="McpToolInvocation"/> to the plugin handler. That gate — not the plugin — carries the
/// entitlement factor, so a workspace that is not effectively available (billing lapse, unhealthy
/// runtime, disabled workspace, …) is dark for this tool even when the plugin is globally active and the
/// caller holds the permission. The separate commercial licensing layer (portal/account) remains a later
/// axis. Every failure is returned as an error <see cref="CallToolResult"/>; no exception escapes to the
/// transport.
/// </summary>
internal sealed class ContributedMcpTool : McpServerTool
{
    private readonly McpToolRegistration _registration;
    private readonly string _pluginId;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Func<string, string, CancellationToken, Task<PluginAvailability>>? _availabilityEvaluator;
    private readonly Tool _protocolTool;

    /// <param name="registration">The SDK-neutral tool this wrapper adapts.</param>
    /// <param name="pluginId">The contributing plugin's id — tracked by the registry as provenance, never part of the plugin-facing contract; used to gate the call through <see cref="IPluginAvailabilityEvaluator"/>.</param>
    /// <param name="httpContextAccessor">Supplies the ambient request principal and (in production) the request services used to resolve the availability evaluator.</param>
    /// <param name="availabilityEvaluator">Optional injected availability check for tests. When null, the evaluator is resolved per call from the request's <see cref="IServiceProvider"/> — the production path — and the check is skipped (fail-open) if none is registered.</param>
    public ContributedMcpTool(
        McpToolRegistration registration,
        string pluginId,
        IHttpContextAccessor httpContextAccessor,
        Func<string, string, CancellationToken, Task<PluginAvailability>>? availabilityEvaluator = null)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        _pluginId = pluginId;
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _availabilityEvaluator = availabilityEvaluator;
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

        // The contributing plugin must be effectively available in the resolved workspace (REV2 §13):
        // an entitlement lapse, unhealthy runtime, disabled workspace or missing capability makes the
        // tool dark here even though the plugin is globally active and the caller holds the permission.
        // Ordering is deliberate: this runs *after* the permission check so it never leaks availability
        // to an unauthorized caller. Mirrors PluginApiEndpointDataSource — fail-open when no evaluator is
        // resolvable, since composition always registers one.
        var availabilityEvaluator = ResolveAvailabilityEvaluator();
        if (availabilityEvaluator is not null)
        {
            var availability = await availabilityEvaluator(_pluginId, workspaceKey, cancellationToken)
                .ConfigureAwait(false);
            if (!availability.IsAvailable)
            {
                return Error("The tool is not available for this workspace.");
            }
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

    // Prefer the directly injected delegate (tests). On the production path resolve the evaluator from
    // the request's service provider — consistent with PluginApiEndpointDataSource — so the same live,
    // scoped instance the HTTP path uses gates the MCP call. Returns null when nothing is resolvable,
    // which the caller treats as fail-open.
    private Func<string, string, CancellationToken, Task<PluginAvailability>>? ResolveAvailabilityEvaluator()
    {
        if (_availabilityEvaluator is not null)
        {
            return _availabilityEvaluator;
        }

        var evaluator = _httpContextAccessor.HttpContext?.RequestServices
            .GetService<IPluginAvailabilityEvaluator>();
        if (evaluator is null)
        {
            return null;
        }

        return evaluator.EvaluateAsync;
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
