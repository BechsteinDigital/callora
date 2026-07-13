namespace Callora.Host.PluginContracts.Application.Http;

/// <summary>
/// Declares one HTTP route on a plugin controller action — the Callora
/// counterpart of Symfony's #[Route] attribute.
/// Action signatures: <c>Task&lt;ApiResult&gt; M(ApiRequest, CancellationToken)</c>
/// or <c>Task M(ApiRequest, ApiEventStream, CancellationToken)</c> for
/// server-sent event streams.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CalloraRouteAttribute(string httpMethod, string pathTemplate) : Attribute
{
    /// <summary>HTTP method, e.g. "GET".</summary>
    public string HttpMethod { get; } = httpMethod;

    /// <summary>Absolute route template, e.g. "/api/calls/{callId}/accept".</summary>
    public string PathTemplate { get; } = pathTemplate;

    /// <summary>Required permission key; empty means authenticated only.</summary>
    public string Permission { get; init; } = string.Empty;

    /// <summary>Optional stable route name.</summary>
    public string? Name { get; init; }
}
