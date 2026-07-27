namespace Callora.Core.Application.Mcp.Contracts;

/// <summary>
/// A plugin's contribution of MCP tools, exported so the host can expose plugin capabilities to
/// out-of-process AI agents over the Model Context Protocol. The contract is SDK-neutral: plugins
/// describe their tools with <see cref="McpToolRegistration"/> and never reference the MCP transport
/// SDK (mirroring how HTTP route registrations keep plugins ASP.NET-neutral). The host aggregates
/// contributors into one live tool collection, adding a plugin's tools on activation and removing
/// them on deactivation.
/// </summary>
public interface IMcpToolContributor
{
    /// <summary>The MCP tools this plugin contributes.</summary>
    IReadOnlyList<McpToolRegistration> Tools { get; }
}
