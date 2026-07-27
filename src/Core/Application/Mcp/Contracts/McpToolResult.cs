using System.Text.Json;

namespace Callora.Core.Application.Mcp.Contracts;

/// <summary>
/// The neutral result of an <see cref="IMcpToolContributor"/> tool handler. It carries a textual
/// payload (typically a JSON string) plus an error flag, kept free of any MCP-SDK type so plugins
/// never reference the transport SDK. The host maps this onto the SDK's call-tool result.
/// </summary>
/// <param name="Content">
/// The tool's textual output. For structured data this is a JSON string produced by <see cref="Json"/>.
/// </param>
/// <param name="IsError">
/// Whether the result represents a tool-level error. Errors are reported to the caller as a failed
/// tool call, never as an unhandled exception crossing the transport boundary.
/// </param>
public sealed record McpToolResult(string Content, bool IsError = false)
{
    /// <summary>Serializes <paramref name="value"/> to JSON and returns it as a successful result.</summary>
    public static McpToolResult Json(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new McpToolResult(JsonSerializer.Serialize(value), IsError: false);
    }

    /// <summary>Returns an error result carrying the given message.</summary>
    public static McpToolResult Error(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new McpToolResult(message, IsError: true);
    }
}
