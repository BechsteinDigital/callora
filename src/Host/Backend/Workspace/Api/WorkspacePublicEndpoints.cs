using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Callora.Host.Backend.Application.Extensions;
using Callora.Host.Backend.Application.Policies;
using System.Text.Json;

namespace Callora.Host.Workspace.Api;

public static class WorkspacePublicEndpoints
{
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

                    var payload = CreateBootstrapPayload(
                        workspace,
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

                    return Results.Ok(new
                    {
                        workspace = new
                        {
                            key = workspace.WorkspaceKey,
                            name = workspace.DisplayName,
                            type = workspace.WorkspaceType
                        },
                        route = new
                        {
                            workspace.PublicBaseUrl,
                            workspace.PublicHost,
                            workspace.PublicPathPrefix
                        }
                    });
                })
            .AllowAnonymous()
            .ExcludeFromDescription();

        endpoints.MapGet(
                "/workspace/public/ui-chain",
                async (
                    string? workspaceKey,
                    WorkspaceUiChainResolver uiChainResolver,
                    BackendHostOptions hostOptions,
                    IWorkspaceManagementStore workspaceStore,
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

                    var chain = await uiChainResolver
                        .ResolveAsync(normalizedKey, cancellationToken)
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
                        valuesByKey = theme?.ValuesByKey ?? new Dictionary<string, string>()
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
                    var workspaceLoginPath = BuildWorkspaceLoginPath(workspace.PublicPathPrefix);

                    var redirectUrl = BuildRedirectUrl(
                        hostOptions.WorkspaceShellBaseUrl,
                        workspaceLoginPath,
                        query);
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

                    var workspaceUrl = BuildRedirectUrl(
                        hostOptions.WorkspaceShellBaseUrl,
                        requestPath,
                        ToSingleValueQueryDictionary(httpContext.Request.Query));
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
                    var workspaceUrl = BuildRedirectUrl(
                        hostOptions.WorkspaceShellBaseUrl,
                        requestPath,
                        ToSingleValueQueryDictionary(httpContext.Request.Query));
                    return Results.Redirect(workspaceUrl);
                })
            .AllowAnonymous()
            .ExcludeFromDescription();
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

    private static object CreateBootstrapPayload(
        WorkspaceSnapshot? workspace,
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
            route = new
            {
                publicBaseUrl = workspace.PublicBaseUrl ?? hostOptions.WorkspaceShellBaseUrl,
                publicPathPrefix = workspace.PublicPathPrefix
            }
        };
    }
}
