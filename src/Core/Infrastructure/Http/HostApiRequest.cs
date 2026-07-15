using System.Security.Claims;
using System.Text.Json;
using Callora.Core.Application.Http.Contracts;

namespace Callora.Core.Infrastructure.Http;

/// <summary>
/// ASP.NET-backed <see cref="ApiRequest"/> handed to plugin controllers.
/// </summary>
public sealed class HostApiRequest(HttpContext httpContext) : ApiRequest
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override ClaimsPrincipal User => httpContext.User;

    public override IReadOnlyDictionary<string, string> RouteValues =>
        httpContext.Request.RouteValues
            .Where(static pair => pair.Value is not null)
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value!.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

    public override IReadOnlyDictionary<string, string> Query =>
        httpContext.Request.Query.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

    public override string? WorkspaceKey
    {
        get
        {
            if (httpContext.Request.Query.TryGetValue("workspaceKey", out var queryValue))
                return queryValue.ToString();

            return httpContext.Request.RouteValues.TryGetValue("workspaceKey", out var routeValue)
                ? routeValue?.ToString()
                : null;
        }
    }

    public override async Task<T?> ReadJsonAsync<T>(CancellationToken cancellationToken = default)
        where T : default
    {
        try
        {
            return await JsonSerializer
                .DeserializeAsync<T>(httpContext.Request.Body, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Leerer oder defekter Body zählt als "kein Body" — die Action
            // entscheidet über die fachliche Antwort.
            return default;
        }
    }
}
