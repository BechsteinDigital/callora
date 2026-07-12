using Callora.Host.Backend.Application.Abstractions.Webhooks;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Backend.Api;

/// <summary>
/// Manage outbound webhook subscriptions. Secrets are write-only: list and
/// get responses never echo them.
/// </summary>
public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks")
            .RequireAuthorization();

        group.MapGet("/", async (
                IWebhookSubscriptionStore store,
                string? workspaceKey,
                CancellationToken cancellationToken) =>
            {
                var subscriptions = await store.ListAsync(workspaceKey, cancellationToken);
                return Results.Ok(subscriptions.Select(ToPublicShape));
            })
            .RequirePermission(BackendPermissionKeys.WebhookRead)
            .RequireWorkspaceScope();

        group.MapPost("/", async (
                IWebhookSubscriptionStore store,
                CreateWebhookSubscriptionApiRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.EventName) ||
                    string.IsNullOrWhiteSpace(request.Secret) ||
                    !Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var target) ||
                    (target.Scheme != Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttp))
                {
                    return Results.BadRequest(new { error = "eventName, secret and an absolute http(s) targetUrl are required." });
                }

                if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, request.WorkspaceKey))
                {
                    return Results.Forbid();
                }

                var created = await store.CreateAsync(
                    request.WorkspaceKey,
                    request.EventName,
                    request.TargetUrl,
                    request.Secret,
                    cancellationToken);
                return Results.Ok(ToPublicShape(created));
            })
            .RequirePermission(BackendPermissionKeys.WebhookManage);

        group.MapPut("/{id:guid}/activation", async (
                IWebhookSubscriptionStore store,
                Guid id,
                bool isActive,
                CancellationToken cancellationToken) =>
                await store.SetActiveAsync(id, isActive, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound())
            .RequirePermission(BackendPermissionKeys.WebhookManage);

        group.MapDelete("/{id:guid}", async (
                IWebhookSubscriptionStore store,
                Guid id,
                CancellationToken cancellationToken) =>
                await store.DeleteAsync(id, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound())
            .RequirePermission(BackendPermissionKeys.WebhookManage);

        return app;
    }

    private static object ToPublicShape(WebhookSubscriptionSnapshot subscription) => new
    {
        subscription.Id,
        subscription.WorkspaceKey,
        subscription.EventName,
        subscription.TargetUrl,
        subscription.IsActive,
        subscription.CreatedAtUtc
    };
}
