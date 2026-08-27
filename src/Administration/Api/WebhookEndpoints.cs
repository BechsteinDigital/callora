using Callora.Core.Api;
using Callora.Core.Application.Events.Business;
using Callora.Core.Application.Security;
using Callora.Core.Application.Webhooks;
using Callora.Core.Infrastructure.Security;

namespace Callora.Administration.Api;

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
                BusinessEventRegistry eventRegistry,
                string? workspaceKey,
                int? limit,
                string? cursor,
                CancellationToken cancellationToken) =>
            {
                var subscriptions = await store.ListAsync(workspaceKey, cancellationToken);
                var knownEvents = eventRegistry.ListDescriptors()
                    .Select(static descriptor => descriptor.EventName)
                    .ToArray();
                var ordered = subscriptions
                    .OrderByDescending(static x => x.CreatedAtUtc)
                    .ThenBy(static x => x.Id)
                    .Select(subscription => ToPublicShape(subscription, knownEvents))
                    .ToArray();
                return Results.Ok(ListPagination.Page(
                    ordered, limit, cursor, static x => x.Id.ToString()));
            })
            .Produces<PagedApiResponse<WebhookSubscriptionApiResponse>>()
            .RequirePermission(BackendPermissionKeys.WebhookRead)
            .RequireWorkspaceScope();

        group.MapPost("/", async (
                IWebhookSubscriptionStore store,
                BusinessEventRegistry eventRegistry,
                CreateWebhookSubscriptionApiRequest request,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.EventName) ||
                    string.IsNullOrWhiteSpace(request.Secret) ||
                    !Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var target) ||
                    (target.Scheme != Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttp))
                {
                    return ApiProblems.BadRequest("eventName, secret and an absolute http(s) targetUrl are required.");
                }

                // Event names travel as HTTP header values — strict allowlist
                // rules out CR/LF header-injection payloads at the source.
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.EventName.Trim(), @"^[\w.\-*]{1,120}$"))
                {
                    return ApiProblems.BadRequest("eventName may only contain letters, digits, dots, dashes and *.");
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
                    request.IncludeSensitiveData,
                    cancellationToken);

                // Die Antwort sagt sofort, ob das Muster etwas trifft. Genau hier ist ein
                // Vertipper noch billig — später fällt er nur dadurch auf, dass etwas NICHT
                // passiert, und das bemerkt niemand.
                var knownEvents = eventRegistry.ListDescriptors()
                    .Select(static descriptor => descriptor.EventName)
                    .ToArray();
                return Results.Created(
                    $"/api/webhooks/{created.Id}",
                    ToPublicShape(created, knownEvents));
            })
            .RequirePermission(BackendPermissionKeys.WebhookManage);

        group.MapPut("/{id:guid}/activation", async (
                IWebhookSubscriptionStore store,
                Guid id,
                bool isActive,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var existing = await store.GetAsync(id, cancellationToken);
                if (existing is null)
                {
                    return Results.NotFound();
                }

                if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, existing.WorkspaceKey))
                {
                    return Results.Forbid();
                }

                await store.SetActiveAsync(id, isActive, cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(BackendPermissionKeys.WebhookManage);

        group.MapDelete("/{id:guid}", async (
                IWebhookSubscriptionStore store,
                Guid id,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var existing = await store.GetAsync(id, cancellationToken);
                if (existing is null)
                {
                    return Results.NotFound();
                }

                if (!WorkspaceScopeEvaluator.HasWorkspaceAccess(httpContext.User, existing.WorkspaceKey))
                {
                    return Results.Forbid();
                }

                await store.DeleteAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequirePermission(BackendPermissionKeys.WebhookManage);

        return app;
    }

    private static WebhookSubscriptionApiResponse ToPublicShape(
        WebhookSubscriptionSnapshot subscription,
        IReadOnlyCollection<string> knownEvents) => new(
        subscription.Id,
        subscription.WorkspaceKey,
        subscription.EventName,
        subscription.TargetUrl,
        subscription.IsActive,
        subscription.IncludeSensitiveData,
        subscription.CreatedAtUtc,
        BusinessEventPattern.MatchesAny(subscription.EventName, knownEvents));
}
