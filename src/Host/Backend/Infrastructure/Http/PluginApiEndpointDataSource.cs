using System.Reflection;
using System.Text.Json;
using Callora.Host.Backend.Api;
using Callora.Host.Backend.Infrastructure.Security;
using Callora.Host.PluginContracts.Application.Http;
using Callora.Hosting.Application.Plugins;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace Callora.Host.Backend.Infrastructure.Http;

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
    public void Refresh()
    {
        var endpoints = BuildEndpoints();
        CancellationTokenSource previousTokenSource;
        lock (_syncLock)
        {
            _endpoints = endpoints;
            previousTokenSource = _changeTokenSource;
            _changeTokenSource = new CancellationTokenSource();
        }

        previousTokenSource.Cancel();
        previousTokenSource.Dispose();
    }

    private IReadOnlyList<Endpoint> BuildEndpoints()
    {
        var endpoints = new List<Endpoint>();
        foreach (var controller in pluginCatalog.GetExports<IApiController>())
        {
            var controllerType = controller.GetType();
            foreach (var method in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var route = method.GetCustomAttribute<CalloraRouteAttribute>();
                if (route is null)
                {
                    continue;
                }

                try
                {
                    endpoints.Add(BuildEndpoint(controller, method, route));
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

    private static Endpoint BuildEndpoint(object controller, MethodInfo method, CalloraRouteAttribute route)
    {
        var isStream = IsStreamAction(method);
        if (!isStream && !IsResultAction(method))
        {
            throw new InvalidOperationException(
                $"Action '{method.Name}' must be Task<ApiResult> M(ApiRequest, CancellationToken) " +
                "or Task M(ApiRequest, ApiEventStream, CancellationToken).");
        }

        var requiresWorkspaceScope = controller is WorkspaceApiController;
        var requestDelegate = BuildRequestDelegate(controller, method, route, requiresWorkspaceScope, isStream);

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

    private static RequestDelegate BuildRequestDelegate(
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
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (!string.IsNullOrWhiteSpace(route.Permission) &&
                !httpContext.User.HasClaim(BackendClaimTypes.Permission, route.Permission) &&
                !httpContext.User.HasClaim(BackendClaimTypes.Permission, "*"))
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var request = new HostApiRequest(httpContext);
            if (requiresWorkspaceScope &&
                !WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, request.WorkspaceKey))
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
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

                return;
            }

            var result = await ((Task<ApiResult>)method.Invoke(
                    controller,
                    [request, httpContext.RequestAborted])!)
                .ConfigureAwait(false);
            await WriteResultAsync(httpContext, result).ConfigureAwait(false);
        };
    }

    private static async Task WriteResultAsync(HttpContext httpContext, ApiResult result)
    {
        if (result.Problem is not null)
        {
            httpContext.Response.StatusCode = result.Problem.Status;
            httpContext.Response.ContentType = "application/problem+json";
            var slug = result.Problem.Title.ToLowerInvariant().Replace(' ', '-');
            await httpContext.Response.WriteAsync(
                JsonSerializer.Serialize(new
                {
                    type = ApiProblems.TypeBaseUri + slug,
                    title = result.Problem.Title,
                    status = result.Problem.Status,
                    detail = result.Problem.Detail
                }, JsonOptions),
                httpContext.RequestAborted).ConfigureAwait(false);
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
