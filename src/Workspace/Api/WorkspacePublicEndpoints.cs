using Callora.Core.Application.Extensions;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using System.Text.Json;

namespace Callora.Workspace.Api;

public static class WorkspacePublicEndpoints
{
    /// <summary>The surface every workspace has — the entrance used when none is named.</summary>
    private const string DefaultSurfaceKey = "default";

    private static readonly string[] ReservedPrefixes =
    [
        "api",
        "swagger",
        "workspace",
        "health",
        "plugin-assets",
        "manifests",
        "_nuxt"
    ];

    public static void MapWorkspacePublicEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/workspace/public/resolve",
                async (
                    HttpContext httpContext,
                    BackendHostOptions hostOptions,
                    IWorkspaceManagementStore workspaceStore,
                    CancellationToken cancellationToken) =>
                {
                    var requestHost = ResolveForwardedHost(httpContext);
                    var requestPath = ResolveForwardedPath(httpContext, fallbackPath: "/");

                    var workspace = await ResolveWorkspaceFromRequestAsync(
                            httpHost: requestHost,
                            requestPath,
                            hostOptions,
                            workspaceStore,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return Results.Ok(new
                    {
                        resolved = workspace is not null,
                        workspaceKey = workspace?.WorkspaceKey
                    });
                })
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapGet(
                "/workspace/public/bootstrap.js",
                async (
                    HttpContext httpContext,
                    BackendHostOptions hostOptions,
                    IWorkspaceManagementStore workspaceStore,
                    CancellationToken cancellationToken) =>
                {
                    var requestHost = ResolveForwardedHost(httpContext);
                    var requestPath = ResolveBootstrapPath(httpContext);
                    var workspace = await ResolveWorkspaceFromRequestAsync(
                            httpHost: requestHost,
                            requestPath,
                            hostOptions,
                            workspaceStore,
                            cancellationToken)
                        .ConfigureAwait(false);

                    var surface = await ResolveSurfaceFromRequestAsync(
                            httpHost: requestHost,
                            requestPath,
                            hostOptions,
                            workspaceStore,
                            cancellationToken)
                        .ConfigureAwait(false);

                    var payload = CreateBootstrapPayload(
                        workspace,
                        surface,
                        requestPath,
                        hostOptions);
                    var payloadJson = JsonSerializer.Serialize(payload);
                    var script = $"window.__CALLORA_WORKSPACE_CONTEXT__ = {payloadJson};";

                    httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
                    return Results.Text(script, "application/javascript; charset=utf-8");
                })
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapGet(
                "/workspace/public/context",
                async (
                    string? path,
                    HttpContext httpContext,
                    BackendHostOptions hostOptions,
                    IWorkspaceManagementStore workspaceStore,
                    CancellationToken cancellationToken) =>
                {
                    var requestHost = ResolveForwardedHost(httpContext);
                    var requestPath = NormalizePath(string.IsNullOrWhiteSpace(path) ? "/" : path);

                    var workspace = await ResolveWorkspaceFromRequestAsync(
                            httpHost: requestHost,
                            requestPath,
                            hostOptions,
                            workspaceStore,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (workspace is null)
                    {
                        return Results.NotFound();
                    }

                    // The route belongs to the surface that served this request.
                    var surface = await ResolveSurfaceFromRequestAsync(
                            httpHost: requestHost,
                            requestPath,
                            hostOptions,
                            workspaceStore,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return Results.Ok(new
                    {
                        workspace = new
                        {
                            key = workspace.WorkspaceKey,
                            name = workspace.DisplayName,
                            type = workspace.WorkspaceType
                        },
                        surface = surface is null ? null : new
                        {
                            key = surface.SurfaceKey,
                            accessMode = surface.AccessMode.ToString()
                        },
                        route = new
                        {
                            PublicBaseUrl = surface?.PublicBaseUrl,
                            PublicHost = surface?.PublicHost,
                            PublicPathPrefix = surface?.PublicPathPrefix ?? "/"
                        }
                    });
                })
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapGet(
                "/workspace/public/ui-chain",
                async (
                    string? workspaceKey,
                    string? surfaceKey,
                    HttpContext httpContext,
                    WorkspaceUiChainResolver uiChainResolver,
                    BackendHostOptions hostOptions,
                    IWorkspaceManagementStore workspaceStore,
                    IWorkspaceSurfaceStore surfaceStore,
                    CancellationToken cancellationToken) =>
                {
                    var normalizedKey = string.IsNullOrWhiteSpace(workspaceKey)
                        ? "default"
                        : workspaceKey.Trim();

                    // Anonymous endpoint — only visible workspaces expose their chain.
                    var workspace = await workspaceStore
                        .GetAsync(normalizedKey, cancellationToken)
                        .ConfigureAwait(false);
                    if (!IsWorkspaceVisibleInTenant(workspace, hostOptions.DefaultTenantKey))
                    {
                        return Results.NotFound();
                    }

                    // The access gate is per surface (ADR-014 §6.1): a Mixed surface has
                    // public routes, so its chain loads anonymously; only an Authenticated
                    // surface requires a caller identity. Without a surfaceKey — or when the
                    // named surface is unknown — the workspace's "default" surface decides,
                    // because that is the entrance such a caller came through.
                    var gateSurfaceKey = string.IsNullOrWhiteSpace(surfaceKey)
                        ? DefaultSurfaceKey
                        : surfaceKey.Trim();
                    var gateSurface = await surfaceStore
                        .GetAsync(normalizedKey, gateSurfaceKey, cancellationToken)
                        .ConfigureAwait(false);
                    gateSurface ??= await surfaceStore
                        .GetAsync(normalizedKey, DefaultSurfaceKey, cancellationToken)
                        .ConfigureAwait(false);

                    // Fail closed: a workspace whose entrance cannot be determined does not
                    // expose its plugin inventory.
                    var requiresAuth = gateSurface is null ||
                        gateSurface.AccessMode == SurfaceAccessMode.Authenticated;

                    // An Authenticated surface (or workspace) does not expose its plugin
                    // inventory to an anonymous caller: it 404s exactly like a non-existent one,
                    // so the chain cannot be enumerated for fingerprinting. An authenticated
                    // caller gets the chain; a browser was already sent to log in by
                    // /surface/render (P4).
                    if (requiresAuth && httpContext.User.Identity?.IsAuthenticated != true)
                    {
                        return Results.NotFound();
                    }

                    var chain = await uiChainResolver
                        .ResolveAsync(normalizedKey, surfaceKey, cancellationToken)
                        .ConfigureAwait(false);
                    return Results.Ok(new
                    {
                        workspaceKey = normalizedKey,
                        chain
                    });
                })
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapGet(
                "/workspace/public/theme",
                async (
                    string? workspaceKey,
                    WorkspacePublicThemeResolver themeResolver,
                    CancellationToken cancellationToken) =>
                {
                    var normalizedKey = string.IsNullOrWhiteSpace(workspaceKey)
                        ? "default"
                        : workspaceKey.Trim();
                    var theme = await themeResolver
                        .ResolveAsync(normalizedKey, cancellationToken)
                        .ConfigureAwait(false);
                    return Results.Ok(new
                    {
                        workspaceKey = normalizedKey,
                        themePluginId = theme?.ThemePluginId,
                        themeVersion = theme?.ThemeVersion,
                        valuesByKey = theme?.ValuesByKey ?? new Dictionary<string, string>(),
                        // Die Sektionslayouts gehören dem Theme (§7.1). Ohne zugewiesenes Theme
                        // gelten die des Basis-Themes: Sonst hätte eine frische Installation
                        // nichts, womit sich etwas komponieren ließe, und ein Editor ohne
                        // Auswahl sähe nach einem Fehler aus statt nach einem fehlenden Theme.
                        sectionLayouts = theme?.SectionLayouts ?? SurfaceBaseSectionLayouts.All
                    });
                })
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapGet(
                "/login",
                async (
                    string? workspaceKey,
                    string? returnUrl,
                    HttpContext httpContext,
                    BackendHostOptions hostOptions,
                    IWorkspaceManagementStore workspaceStore,
                    CancellationToken cancellationToken) =>
                {
                    WorkspaceSnapshot workspace;
                    if (!string.IsNullOrWhiteSpace(workspaceKey))
                    {
                        var explicitWorkspace = await workspaceStore
                            .GetAsync(workspaceKey.Trim(), cancellationToken)
                            .ConfigureAwait(false);
                        if (!IsWorkspaceVisibleInTenant(explicitWorkspace, hostOptions.DefaultTenantKey))
                        {
                            var notFoundRedirect = BuildWorkspaceNotFoundRedirectUrl(hostOptions.WorkspaceShellBaseUrl);
                            return Results.Redirect(notFoundRedirect);
                        }

                        workspace = explicitWorkspace!;
                    }
                    else
                    {
                        var loginReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl!;
                        var resolvedWorkspace = await ResolveWorkspaceFromRequestAsync(
                                httpHost: httpContext.Request.Host.Value,
                                requestPath: loginReturnUrl,
                                hostOptions,
                                workspaceStore,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (resolvedWorkspace is null)
                        {
                            var notFoundRedirect = BuildWorkspaceNotFoundRedirectUrl(hostOptions.WorkspaceShellBaseUrl);
                            return Results.Redirect(notFoundRedirect);
                        }

                        workspace = resolvedWorkspace;
                    }

                    var query = ToSingleValueQueryDictionary(httpContext.Request.Query);
                    query["returnUrl"] = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
                    var loginSurface = await ResolveSurfaceFromRequestAsync(
                            httpHost: ResolveForwardedHost(httpContext),
                            NormalizePath(returnUrl),
                            hostOptions,
                            workspaceStore,
                            cancellationToken)
                        .ConfigureAwait(false);
                    var workspaceLoginPath = BuildWorkspaceLoginPath(loginSurface?.PublicPathPrefix ?? "/");

                    var redirectUrl = BuildRedirectUrl(
                        hostOptions.WorkspaceShellBaseUrl,
                        workspaceLoginPath,
                        query);
                    if (WouldSelfRedirect(redirectUrl, httpContext.Request.Path.Value ?? workspaceLoginPath))
                    {
                        return Results.Redirect(BuildAdminShellRedirectUrl(hostOptions, query));
                    }

                    return Results.Redirect(redirectUrl);
                })
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapGet(
                "/",
                async (
                    HttpContext httpContext,
                    BackendHostOptions hostOptions,
                    IWorkspaceManagementStore workspaceStore,
                    CancellationToken cancellationToken) =>
                {
                    const string requestPath = "/";
                    var workspace = await ResolveWorkspaceFromRequestAsync(
                            httpHost: httpContext.Request.Host.Value,
                            requestPath,
                            hostOptions,
                            workspaceStore,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (workspace is null)
                    {
                        var notFoundRedirect = BuildWorkspaceNotFoundRedirectUrl(hostOptions.WorkspaceShellBaseUrl);
                        return Results.Redirect(notFoundRedirect);
                    }

                    var query = ToSingleValueQueryDictionary(httpContext.Request.Query);
                    var workspaceUrl = BuildRedirectUrl(
                        hostOptions.WorkspaceShellBaseUrl,
                        requestPath,
                        query);
                    if (WouldSelfRedirect(workspaceUrl, requestPath))
                    {
                        return Results.Redirect(BuildAdminShellRedirectUrl(hostOptions, query));
                    }

                    return Results.Redirect(workspaceUrl);
                })
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapGet(
                "/{**path:nonfile}",
                async (
                    HttpContext httpContext,
                    string? path,
                    BackendHostOptions hostOptions,
                    IWorkspaceManagementStore workspaceStore,
                    CancellationToken cancellationToken) =>
                {
                    var requestPath = "/" + (path ?? string.Empty);

                    // The not-found sink is terminal. When the workspace shell is
                    // hosted same-origin (WorkspaceShellBaseUrl is a local path, e.g.
                    // "/"), an unresolved request redirects to the 404 path — which
                    // lands back on this catch-all and, still unresolved, would
                    // redirect to itself forever. Serve a real 404 instead. With an
                    // external shell base the redirect leaves this origin, so the
                    // request never returns here and this guard does not trigger.
                    if (IsWorkspaceNotFoundPath(requestPath, hostOptions.WorkspaceShellBaseUrl))
                    {
                        return Results.NotFound();
                    }

                    if (IsAdminPath(requestPath))
                    {
                        var adminRelativePath = requestPath["/admin".Length..];
                        if (string.IsNullOrWhiteSpace(adminRelativePath))
                        {
                            adminRelativePath = "/";
                        }

                        var redirectUrl = BuildRedirectUrl(
                            hostOptions.AdminShellBaseUrl,
                            adminRelativePath,
                            ToSingleValueQueryDictionary(httpContext.Request.Query));
                        return Results.Redirect(redirectUrl);
                    }

                    if (IsReservedPath(requestPath))
                    {
                        return Results.NotFound();
                    }

                    var workspace = await ResolveWorkspaceFromRequestAsync(
                            httpHost: httpContext.Request.Host.Value,
                            requestPath,
                            hostOptions,
                            workspaceStore,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (workspace is null)
                    {
                        var notFoundRedirect = BuildWorkspaceNotFoundRedirectUrl(hostOptions.WorkspaceShellBaseUrl);
                        return Results.Redirect(notFoundRedirect);
                    }
                    var query = ToSingleValueQueryDictionary(httpContext.Request.Query);
                    var workspaceUrl = BuildRedirectUrl(
                        hostOptions.WorkspaceShellBaseUrl,
                        requestPath,
                        query);
                    if (WouldSelfRedirect(workspaceUrl, requestPath))
                    {
                        return Results.Redirect(BuildAdminShellRedirectUrl(hostOptions, query));
                    }

                    return Results.Redirect(workspaceUrl);
                })
            .AllowAnonymous()
            .ExcludeFromDescription()
            // In a colocated host Surface.Rendering owns public non-file paths.
            // Keeping this fallback at a later order lets concrete Workspace/API
            // routes win normally and leaves the redirect available to hosts that
            // do not compose Surface.Rendering.
            .WithOrder(100);
    }

    private static string ResolveForwardedHost(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost) &&
            !string.IsNullOrWhiteSpace(forwardedHost))
        {
            return forwardedHost.ToString() ?? string.Empty;
        }

        return httpContext.Request.Host.Value ?? string.Empty;
    }

    private static string ResolveForwardedPath(HttpContext httpContext, string fallbackPath)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-Uri", out var forwardedUri) &&
            !string.IsNullOrWhiteSpace(forwardedUri))
        {
            var forwardedUriValue = forwardedUri.ToString().Trim();
            if (Uri.TryCreate(forwardedUriValue, UriKind.Absolute, out var absoluteUri))
            {
                return NormalizePath(absoluteUri.AbsolutePath);
            }

            var separatorIndex = forwardedUriValue.IndexOf('?', StringComparison.Ordinal);
            var rawPath = separatorIndex >= 0
                ? forwardedUriValue[..separatorIndex]
                : forwardedUriValue;

            return NormalizePath(rawPath);
        }

        return NormalizePath(fallbackPath);
    }

    private static string ResolveBootstrapPath(HttpContext httpContext)
    {
        var explicitPath = httpContext.Request.Query.TryGetValue("path", out var pathValues)
            ? pathValues.ToString()
            : null;
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return NormalizePath(explicitPath);
        }

        if (httpContext.Request.Headers.TryGetValue("Referer", out var refererValues))
        {
            var refererValue = refererValues.ToString();
            if (!string.IsNullOrWhiteSpace(refererValue) &&
                Uri.TryCreate(refererValue, UriKind.Absolute, out var refererUri))
            {
                return NormalizePath(refererUri.AbsolutePath);
            }
        }

        return "/";
    }

    private static bool IsAdminPath(string requestPath)
    {
        var normalized = requestPath.TrimStart('/');
        return normalized.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("admin/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReservedPath(string requestPath)
    {
        var normalized = requestPath.TrimStart('/');
        foreach (var prefix in ReservedPrefixes)
        {
            if (normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<WorkspaceSnapshot?> ResolveWorkspaceFromRequestAsync(
        string? httpHost,
        string requestPath,
        BackendHostOptions hostOptions,
        IWorkspaceManagementStore workspaceStore,
        CancellationToken cancellationToken)
    {
        var tenantKey = string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey)
            ? null
            : hostOptions.DefaultTenantKey.Trim();

        var requestHost = string.IsNullOrWhiteSpace(httpHost)
            ? string.Empty
            : httpHost.Trim().ToLowerInvariant();

        var workspace = await workspaceStore
            .ResolveByPublicRouteAsync(requestHost, requestPath, tenantKey, cancellationToken)
            .ConfigureAwait(false);

        return IsWorkspaceVisibleInTenant(workspace, tenantKey) ? workspace : null;
    }

    /// <summary>
    /// The surface that serves this request. Public routing resolves per surface
    /// (ADR-014 §5) — the workspace itself has no address, so anything that needs
    /// a route (bootstrap payload, login redirect) asks here.
    /// </summary>
    private static async Task<WorkspaceSurfaceSnapshot?> ResolveSurfaceFromRequestAsync(
        string? httpHost,
        string requestPath,
        BackendHostOptions hostOptions,
        IWorkspaceManagementStore workspaceStore,
        CancellationToken cancellationToken)
    {
        var tenantKey = string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey)
            ? null
            : hostOptions.DefaultTenantKey.Trim();

        var requestHost = string.IsNullOrWhiteSpace(httpHost)
            ? string.Empty
            : httpHost.Trim().ToLowerInvariant();

        return await workspaceStore
            .ResolveSurfaceByPublicRouteAsync(requestHost, requestPath, tenantKey, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsWorkspaceVisibleInTenant(WorkspaceSnapshot? workspace, string? tenantKey)
    {
        if (workspace is null || !workspace.IsActive || !workspace.TenantIsActive)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return true;
        }

        return string.Equals(workspace.TenantKey, tenantKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string?> ToSingleValueQueryDictionary(IQueryCollection query)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query)
        {
            result[pair.Key] = pair.Value.Count > 0 ? pair.Value[0] : null;
        }

        return result;
    }

    private static string BuildRedirectUrl(
        string shellBaseUrl,
        string requestPath,
        IReadOnlyDictionary<string, string?> query)
    {
        var safeBase = string.IsNullOrWhiteSpace(shellBaseUrl) ? "/" : shellBaseUrl.Trim();
        var safePath = string.IsNullOrWhiteSpace(requestPath) ? "/" : requestPath.Trim();
        if (!safePath.StartsWith('/'))
        {
            safePath = "/" + safePath;
        }

        if ((safeBase.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             safeBase.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
            Uri.TryCreate(safeBase, UriKind.Absolute, out var absoluteBaseUri))
        {
            var mergedPath = MergePaths(absoluteBaseUri.AbsolutePath, safePath);
            var builder = new UriBuilder(absoluteBaseUri)
            {
                Path = mergedPath,
                Query = QueryString.Create(query).Value?.TrimStart('?')
            };
            return builder.Uri.ToString();
        }

        var mergedRelativePath = MergePaths(safeBase, safePath);
        var queryString = QueryString.Create(query).Value;
        return string.Concat(mergedRelativePath, queryString);
    }

    private static string MergePaths(string basePath, string requestPath)
    {
        var normalizedBase = string.IsNullOrWhiteSpace(basePath)
            ? "/"
            : basePath.Replace('\\', '/').Trim();

        var normalizedRequestPath = string.IsNullOrWhiteSpace(requestPath)
            ? "/"
            : requestPath.Replace('\\', '/').Trim();

        if (!normalizedBase.StartsWith('/'))
        {
            normalizedBase = "/" + normalizedBase;
        }

        normalizedBase = normalizedBase.TrimEnd('/');
        if (normalizedBase.Length == 0)
        {
            normalizedBase = "/";
        }

        if (!normalizedRequestPath.StartsWith('/'))
        {
            normalizedRequestPath = "/" + normalizedRequestPath;
        }

        if (normalizedBase == "/")
        {
            return normalizedRequestPath;
        }

        if (normalizedRequestPath == "/")
        {
            return normalizedBase + "/";
        }

        return normalizedBase + normalizedRequestPath;
    }

    private static string BuildWorkspaceLoginPath(string? workspacePublicPathPrefix)
    {
        var prefix = NormalizePath(workspacePublicPathPrefix);
        if (prefix == "/")
        {
            return "/login";
        }

        return prefix + "/login";
    }

    private static string BuildWorkspaceNotFoundRedirectUrl(string workspaceShellBaseUrl)
    {
        return BuildRedirectUrl(
            workspaceShellBaseUrl,
            "/404",
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildAdminShellRedirectUrl(
        BackendHostOptions hostOptions,
        IReadOnlyDictionary<string, string?> query) =>
        BuildRedirectUrl(hostOptions.AdminShellBaseUrl, "/", query);

    // True when a same-origin workspace-shell redirect would target the very path being served —
    // a self-redirect loop. Happens when WorkspaceShellBaseUrl is a local path (e.g. "/") that
    // resolves back onto this handler. Absolute (external-shell) targets leave this origin, so they
    // never self-redirect and are excluded.
    private static bool WouldSelfRedirect(string redirectTarget, string requestPath)
    {
        if (redirectTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            redirectTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var targetPath = redirectTarget.Split('?', '#')[0];
        return string.Equals(
            NormalizePath(targetPath),
            NormalizePath(requestPath),
            StringComparison.OrdinalIgnoreCase);
    }

    // True when the request already targets the not-found sink for a same-origin
    // shell base — the guard that keeps the 404 redirect from looping onto itself.
    private static bool IsWorkspaceNotFoundPath(string requestPath, string workspaceShellBaseUrl)
    {
        var notFoundTarget = BuildWorkspaceNotFoundRedirectUrl(workspaceShellBaseUrl);
        return string.Equals(
            NormalizePath(requestPath),
            NormalizePath(notFoundTarget),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "/";
        }

        var path = input.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        while (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
        {
            path = path[..^1];
        }

        return path;
    }

    // The workspace names the data container, the surface carries the route it is
    // reached through — the payload needs both.
    private static object CreateBootstrapPayload(
        WorkspaceSnapshot? workspace,
        WorkspaceSurfaceSnapshot? surface,
        string requestPath,
        BackendHostOptions hostOptions)
    {
        if (workspace is null)
        {
            return new
            {
                workspace = new
                {
                    key = "default",
                    name = "Workspace",
                    type = "base"
                },
                surface = (object?)null,
                route = new
                {
                    publicBaseUrl = hostOptions.WorkspaceShellBaseUrl,
                    publicPathPrefix = requestPath
                }
            };
        }

        return new
        {
            workspace = new
            {
                key = workspace.WorkspaceKey,
                name = workspace.DisplayName,
                type = workspace.WorkspaceType
            },
            surface = surface is null ? null : new
            {
                key = surface.SurfaceKey,
                accessMode = surface.AccessMode.ToString()
            },
            route = new
            {
                publicBaseUrl = surface?.PublicBaseUrl ?? hostOptions.WorkspaceShellBaseUrl,
                publicPathPrefix = surface?.PublicPathPrefix ?? requestPath
            }
        };
    }
}
