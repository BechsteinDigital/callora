using Callora.Core.Api;
using Callora.Core.Application.Http.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Security;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Reflection;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Http;

/// <summary>
/// Dynamic routing source for plugin controllers (PLAT-257): builds
/// endpoints from every exported <see cref="IApiController"/> and refreshes
/// them on plugin lifecycle changes, so routes appear on activation and
/// vanish on deactivation (hot swap). Enforcement per route: authentication,
/// the declared permission, and workspace scope for
/// <see cref="WorkspaceApiController"/> descendants.
/// </summary>
public sealed class PluginApiEndpointDataSource(
    ICalloraPluginCatalog pluginCatalog,
    ILogger<PluginApiEndpointDataSource> logger) : EndpointDataSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly object _syncLock = new();

    // Getrennt von _syncLock, weil er über den gesamten Neubau gehalten wird: Leser dürfen
    // dabei nicht warten. Siehe Refresh().
    private readonly object _rebuildLock = new();
    private IReadOnlyList<Endpoint> _endpoints = [];
    private CancellationTokenSource _changeTokenSource = new();

    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            lock (_syncLock)
            {
                return _endpoints;
            }
        }
    }

    public override IChangeToken GetChangeToken()
    {
        lock (_syncLock)
        {
            return new CancellationChangeToken(_changeTokenSource.Token);
        }
    }

    /// <summary>Rebuilds all plugin endpoints and signals the routing system.</summary>
    /// <remarks>
    /// Der Neubau steht MIT im Lock, nicht davor. Vorher las jeder Aufrufer den Katalog frei und
    /// schrieb sein Ergebnis danach — bei zwei Lebenszyklus-Ereignissen kurz nacheinander gewann
    /// der langsamere Bau, obwohl er den älteren Katalog gelesen hatte. Ein deaktiviertes Plugin
    /// behielt dann seine Routen bis zum nächsten Ereignis, und ihr Delegat hielt dessen
    /// Controller-Instanz aus einem bereits entladenen Ladekontext am Leben. Dieselbe Klasse von
    /// Fehler wie der stehengebliebene Export in #253: nicht die einzelne Operation ist unsicher,
    /// sondern die Folge aus Lesen und Schreiben.
    /// <para>
    /// Serialisieren kostet hier nichts: Refresh läuft nur auf Lebenszyklus-Ereignisse, und Leser
    /// nehmen <c>_syncLock</c>, nicht diesen — sie warten also nicht auf den Neubau mit.
    /// </para>
    /// </remarks>
    public void Refresh()
    {
        CancellationTokenSource previousTokenSource;
        lock (_rebuildLock)
        {
            var endpoints = BuildEndpoints();
            lock (_syncLock)
            {
                _endpoints = endpoints;
                previousTokenSource = _changeTokenSource;
                _changeTokenSource = new CancellationTokenSource();
            }
        }

        // Außerhalb beider Locks: Die Callbacks des Routers laufen synchron und lesen dabei
        // Endpoints — unter _syncLock wäre das ein Deadlock.
        previousTokenSource.Cancel();
        previousTokenSource.Dispose();
    }

    private IReadOnlyList<Endpoint> BuildEndpoints()
    {
        var endpoints = new List<Endpoint>();
        var claimedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var owned in pluginCatalog.GetOwnedExports(typeof(IApiController)))
        {
            if (owned.Service is not IApiController controller)
            {
                continue;
            }

            var pluginId = owned.PluginId;
            var controllerType = controller.GetType();
            foreach (var method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var route = method.GetCustomAttribute<CalloraRouteAttribute>();
                if (route is null)
                {
                    continue;
                }

                if (ReservedHostRoutePrefixes.Collides(route.PathTemplate))
                {
                    logger.LogWarning(
                        "Rejected plugin route {Method} {Path} on {ControllerType}: it collides with a reserved host route namespace.",
                        route.HttpMethod,
                        route.PathTemplate,
                        controllerType.FullName);
                    continue;
                }

                // Kollisionsschutz zwischen Plugins (first-wins): zwei Plugins mit
                // identischer Methode+Route würden sonst zur Request-Zeit in eine
                // AmbiguousMatchException laufen statt bei der Registrierung.
                var routeKey = $"{route.HttpMethod}:{route.PathTemplate.Trim('/')}";
                if (claimedRoutes.Contains(routeKey))
                {
                    logger.LogWarning(
                        "Rejected plugin route {Method} {Path} on {ControllerType}: another plugin already registered this route (first-wins).",
                        route.HttpMethod,
                        route.PathTemplate,
                        controllerType.FullName);
                    continue;
                }

                try
                {
                    endpoints.Add(BuildEndpoint(pluginId, controller, method, route));
                    claimedRoutes.Add(routeKey);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Plugin route {Method} {Path} on {ControllerType} could not be mapped.",
                        route.HttpMethod,
                        route.PathTemplate,
                        controllerType.FullName);
                }
            }
        }

        logger.LogInformation("Plugin routing refreshed: {EndpointCount} endpoints.", endpoints.Count);
        return endpoints;
    }

    private Endpoint BuildEndpoint(string pluginId, object controller, MethodInfo method, CalloraRouteAttribute route)
    {
        var isStream = IsStreamAction(method);
        if (!isStream && !IsResultAction(method))
        {
            throw new InvalidOperationException(
                $"Action '{method.Name}' must be Task<ApiResult> M(ApiRequest, CancellationToken) " +
                "or Task M(ApiRequest, ApiEventStream, CancellationToken).");
        }

        var requiresWorkspaceScope = controller is WorkspaceApiController;
        var requestDelegate = BuildRequestDelegate(pluginId, controller, method, route, requiresWorkspaceScope, isStream);

        var builder = new RouteEndpointBuilder(
            requestDelegate,
            RoutePatternFactory.Parse(route.PathTemplate),
            order: 0)
        {
            DisplayName = route.Name ?? $"{controller.GetType().Name}.{method.Name}"
        };
        builder.Metadata.Add(new HttpMethodMetadata([route.HttpMethod.ToUpperInvariant()]));
        return builder.Build();
    }

    private RequestDelegate BuildRequestDelegate(
        string pluginId,
        object controller,
        MethodInfo method,
        CalloraRouteAttribute route,
        bool requiresWorkspaceScope,
        bool isStream)
    {
        return async httpContext =>
        {
            if (httpContext.User.Identity?.IsAuthenticated != true)
            {
                await WriteProblemAsync(httpContext, StatusCodes.Status401Unauthorized,
                    "Unauthorized", "Authentication is required.").ConfigureAwait(false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(route.Permission) &&
                !httpContext.User.HasClaim(BackendClaimTypes.Permission, route.Permission) &&
                !httpContext.User.HasClaim(BackendClaimTypes.Permission, "*"))
            {
                await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden,
                    "Forbidden", $"The permission '{route.Permission}' is required.").ConfigureAwait(false);
                return;
            }

            var request = new HostApiRequest(httpContext);
            if (requiresWorkspaceScope &&
                !WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, request.WorkspaceKey))
            {
                await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden,
                    "Forbidden", "The caller is not scoped to the requested workspace.").ConfigureAwait(false);
                return;
            }

            // A workspace-scoped route only serves when the plugin is effectively
            // available in that workspace (REV2 §13): an entitlement lapse, missing
            // capability, unhealthy runtime or inactive workspace returns 403 rather
            // than letting the request reach a plugin that should be dark.
            if (requiresWorkspaceScope && !string.IsNullOrWhiteSpace(request.WorkspaceKey))
            {
                // Fails closed when the evaluator is missing. It used to fail open, on the
                // reasoning that availability is an extra layer over the always-enforced
                // auth/permission/scope checks — true of a permission, false of this one:
                // availability carries the ENTITLEMENT factor, so failing open serves a
                // plugin the workspace stopped paying for, and does it exactly when the
                // composition is already broken. AddCalloraHost always registers the
                // evaluator, so its absence is a broken host, not a minimal one.
                var availabilityEvaluator =
                    httpContext.RequestServices.GetService<IPluginAvailabilityEvaluator>();
                if (availabilityEvaluator is null)
                {
                    // 503, not 403: the host cannot answer the question, which is a different
                    // fact from "you may not" — and a 403 would send an operator hunting an
                    // entitlement problem that does not exist.
                    await WriteProblemAsync(httpContext, StatusCodes.Status503ServiceUnavailable,
                        "Service Unavailable",
                        "The plugin availability gate is not configured on this host.").ConfigureAwait(false);
                    return;
                }

                var availability = await availabilityEvaluator
                    .EvaluateAsync(pluginId, request.WorkspaceKey, httpContext.RequestAborted)
                    .ConfigureAwait(false);
                if (!availability.IsAvailable)
                {
                    await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden,
                        "Forbidden", $"The plugin '{pluginId}' is not available in this workspace.").ConfigureAwait(false);
                    return;
                }
            }

            if (isStream)
            {
                httpContext.Response.Headers.ContentType = "text/event-stream";
                httpContext.Response.Headers.CacheControl = "no-cache";
                httpContext.Response.Headers.Append("X-Accel-Buffering", "no");
                await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted).ConfigureAwait(false);

                try
                {
                    await ((Task)method.Invoke(
                            controller,
                            [request, new HostApiEventStream(httpContext), httpContext.RequestAborted])!)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Consumer disconnected — expected stream end.
                }
                catch (Exception exception)
                {
                    // The response has already started, so the status cannot be
                    // rewritten — a plugin fault must still be recorded, not swallowed.
                    logger.LogError(Unwrap(exception),
                        "Plugin stream action {Action} on {Path} failed.", method.Name, route.PathTemplate);
                }

                return;
            }

            ApiResult result;
            try
            {
                result = await ((Task<ApiResult>)method.Invoke(
                        controller,
                        [request, httpContext.RequestAborted])!)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return; // Client disconnected before the action completed.
            }
            catch (Exception exception)
            {
                logger.LogError(Unwrap(exception),
                    "Plugin action {Action} on {Path} threw.", method.Name, route.PathTemplate);
                await WriteProblemAsync(httpContext, StatusCodes.Status500InternalServerError,
                    "Plugin error", "The plugin action failed to process the request.").ConfigureAwait(false);
                return;
            }

            await WriteResultAsync(httpContext, result).ConfigureAwait(false);
        };
    }

    /// <summary>Unwraps the reflection wrapper so logs show the real plugin fault.</summary>
    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;

    private static async Task WriteProblemAsync(HttpContext httpContext, int status, string title, string? detail)
    {
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        var slug = title.ToLowerInvariant().Replace(' ', '-');
        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(new
            {
                type = ApiProblems.TypeBaseUri + slug,
                title,
                status,
                detail
            }, JsonOptions),
            httpContext.RequestAborted).ConfigureAwait(false);
    }

    private static async Task WriteResultAsync(HttpContext httpContext, ApiResult result)
    {
        if (result.Problem is not null)
        {
            await WriteProblemAsync(httpContext, result.Problem.Status, result.Problem.Title, result.Problem.Detail)
                .ConfigureAwait(false);
            return;
        }

        httpContext.Response.StatusCode = result.StatusCode;
        if (!string.IsNullOrWhiteSpace(result.Location))
        {
            httpContext.Response.Headers.Location = result.Location;
        }

        if (result.Body is not null)
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync(
                JsonSerializer.Serialize(result.Body, JsonOptions),
                httpContext.RequestAborted).ConfigureAwait(false);
        }
    }

    private static bool IsResultAction(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return method.ReturnType == typeof(Task<ApiResult>) &&
               parameters.Length == 2 &&
               parameters[0].ParameterType == typeof(ApiRequest) &&
               parameters[1].ParameterType == typeof(CancellationToken);
    }

    private static bool IsStreamAction(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return method.ReturnType == typeof(Task) &&
               parameters.Length == 3 &&
               parameters[0].ParameterType == typeof(ApiRequest) &&
               parameters[1].ParameterType == typeof(ApiEventStream) &&
               parameters[2].ParameterType == typeof(CancellationToken);
    }
}
