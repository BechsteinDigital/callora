using System.Collections.Concurrent;
using Callora.Core.Application.Mcp.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;

namespace Callora.Core.Infrastructure.Mcp;

/// <summary>
/// Keeps the host's live MCP tool collection in sync with the active plugin catalog. Activating a
/// plugin registers its <see cref="IMcpToolContributor"/> tools into the shared collection;
/// deactivating removes exactly that plugin's tools. Each mutation of the collection raises its
/// <c>Changed</c> event, which the MCP server surfaces to clients as <c>tools/list_changed</c>. The
/// registry mutates the same <see cref="McpServerPrimitiveCollection{T}"/> instance the MCP server
/// serves, so newly activated tools are live without a restart, re-mount or dropped connection.
/// </summary>
/// <remarks>Thread-safe: per-plugin tracking uses a concurrent map and the SDK collection is itself thread-safe.</remarks>
public sealed class McpToolRegistry : IMcpToolRegistry
{
    private readonly McpServerPrimitiveCollection<McpServerTool> _tools;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<McpToolRegistry> _logger;
    private readonly ConcurrentDictionary<string, IReadOnlyList<McpServerTool>> _byPlugin =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _mutationGate = new();

    /// <summary>Creates a registry over the given live tool collection.</summary>
    /// <param name="tools">The collection the MCP server serves; the registry adds to and removes from it.</param>
    /// <param name="httpContextAccessor">Provides the ambient request principal for per-call authorization.</param>
    /// <param name="logger">Logs contributor failures; defaults to a no-op logger.</param>
    public McpToolRegistry(
        McpServerPrimitiveCollection<McpServerTool> tools,
        IHttpContextAccessor httpContextAccessor,
        ILogger<McpToolRegistry>? logger = null)
    {
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? NullLogger<McpToolRegistry>.Instance;
    }

    /// <summary>
    /// Registers the plugin's contributed tools into the live collection. Re-registering the same plugin
    /// first removes its previously registered tools so the operation is idempotent.
    /// </summary>
    public void Register(string pluginId, IMcpToolContributor contributor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(contributor);

        lock (_mutationGate)
        {
            RemoveTracked(pluginId);

            var added = new List<McpServerTool>();
            foreach (var registration in contributor.Tools)
            {
                if (registration is null)
                {
                    continue;
                }

                // The registry tracks the contributing plugin's id as provenance and threads it into the
                // wrapper so each call is gated through the plugin's per-workspace availability. The
                // public McpToolRegistration contract stays free of plugin identity.
                var tool = new ContributedMcpTool(registration, pluginId, _httpContextAccessor);
                if (_tools.TryAdd(tool))
                {
                    added.Add(tool);
                }
                else
                {
                    _logger.LogError(
                        "Plugin {PluginId} contributed an MCP tool named '{ToolName}' that collides with an existing tool; it was skipped.",
                        pluginId,
                        registration.Name);
                }
            }

            _byPlugin[pluginId] = added;
        }
    }

    /// <summary>
    /// Removes the plugin's tools from the live collection (idempotent if it registered none), so its
    /// tools immediately stop being advertised when it is deactivated.
    /// </summary>
    public void Deregister(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        lock (_mutationGate)
        {
            RemoveTracked(pluginId);
        }
    }

    // Each Remove raises the collection's Changed event; a plugin that registered no tools is a no-op.
    private void RemoveTracked(string pluginId)
    {
        if (!_byPlugin.TryRemove(pluginId, out var tools) || tools.Count == 0)
        {
            return;
        }

        foreach (var tool in tools)
        {
            _tools.Remove(tool);
        }
    }
}
