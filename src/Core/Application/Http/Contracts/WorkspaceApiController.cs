namespace Callora.Core.Application.Http.Contracts;

/// <summary>
/// Route scope "workspace api": workspace-facing routes. In addition to
/// authentication and the declared permission, the host enforces workspace
/// scope — the caller's session must have access to the workspaceKey
/// carried by the request (query or route value).
/// </summary>
public abstract class WorkspaceApiController : CalloraApiController;
