namespace Callora.Core.Application.Http.Contracts;

/// <summary>
/// Route scope "admin api": operator-facing routes. The host requires an
/// authenticated session plus the route's declared permission.
/// </summary>
public abstract class AdminApiController : CalloraApiController;
