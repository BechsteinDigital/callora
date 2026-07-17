using Callora.Core.Infrastructure.Security;

namespace Callora.Administration.Api;

/// <summary>
/// The unified admin context endpoint (ADR-014 §3.3): one authenticated call the
/// admin shell uses to resolve who the caller is and what they may do — identity,
/// effective roles/permissions, scope and workspace binding. Navigation and
/// visibility derive from this; server-side authorization stays authoritative.
/// </summary>
public static class AdminContextEndpoints
{
    public static IEndpointRouteBuilder MapAdminContextEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin Context")
            .RequireAuthorization();

        group.MapGet("/context", (HttpContext httpContext) =>
            {
                var context = AdminContextView.FromPrincipal(httpContext.User);
                return context is null ? Results.Unauthorized() : Results.Ok(context);
            })
            .WithName("Admin_Context")
            .Produces<AdminContextView>();

        return app;
    }
}
