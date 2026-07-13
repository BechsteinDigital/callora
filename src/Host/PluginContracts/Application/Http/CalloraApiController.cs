namespace Callora.Host.PluginContracts.Application.Http;

/// <summary>
/// Shared helpers for plugin API controllers — the Callora counterpart of
/// Symfony's AbstractController. Derive from
/// <see cref="AdminApiController"/> or <see cref="WorkspaceApiController"/>,
/// never from this class directly: the concrete base class is the route
/// scope.
/// </summary>
public abstract class CalloraApiController : IApiController
{
    /// <summary>200 with an optional body.</summary>
    protected static ApiResult Ok(object? body = null) => ApiResult.Ok(body);

    /// <summary>201 with Location header and body.</summary>
    protected static ApiResult Created(string location, object? body = null) => ApiResult.Created(location, body);

    /// <summary>204 without body.</summary>
    protected static ApiResult NoContent() => ApiResult.NoContent();

    /// <summary>400 problem.</summary>
    protected static ApiResult BadRequest(string detail) => ApiResult.BadRequest(detail);

    /// <summary>403 without body.</summary>
    protected static ApiResult Forbidden() => ApiResult.Forbidden();

    /// <summary>404 problem.</summary>
    protected static ApiResult NotFound(string detail) => ApiResult.NotFound(detail);

    /// <summary>409 problem.</summary>
    protected static ApiResult Conflict(string detail) => ApiResult.Conflict(detail);
}
