using System.Security.Claims;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Application.Surfaces;
using Microsoft.AspNetCore.Http;

namespace Callora.Core.Infrastructure.Surfaces;

/// <summary>
/// Derives a surface identity from the backend principal already on the request
/// (ADR-017 §7). Internal users — an agent at their desktop, a doctor at a practice
/// workstation — are platform users; making them authenticate a second time through
/// a plugin would be absurd.
/// <para>
/// What it deliberately does <strong>not</strong> carry is admin permissions. If they
/// travelled as surface claims, a plugin would eventually check them and grant a
/// visitor rights that were only ever meant for the backend.
/// </para>
/// </summary>
public sealed class BackendPrincipalSurfaceIdentitySource(
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider)
    : ISurfaceHostIdentitySource
{
    /// <summary>Claim carrying the workspace a host-derived caller belongs to.</summary>
    public const string WorkspaceClaim = "host.workspace-key";

    /// <inheritdoc />
    public ValueTask<HostSurfaceIdentityResult> AuthenticateAsync(
        HostSurfaceIdentityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (httpContextAccessor.HttpContext?.User is not { Identity.IsAuthenticated: true } principal)
        {
            return ValueTask.FromResult(HostSurfaceIdentityResult.Anonymous);
        }

        if (ResolveSubjectId(principal) is not { } subjectId)
        {
            return ValueTask.FromResult(HostSurfaceIdentityResult.Anonymous);
        }

        var now = timeProvider.GetUtcNow();
        var boundWorkspace = principal.FindFirstValue(BackendClaimTypes.WorkspaceKey);
        var claims = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(boundWorkspace))
        {
            claims[WorkspaceClaim] = [boundWorkspace];
        }

        return ValueTask.FromResult(HostSurfaceIdentityResult.Identified(
            SurfaceIdentityIssuers.Host,
            subjectId,
            "backend-session",
            now,
            // The derived identity lives no longer than the request's own session
            // horizon; the host clamps it again on normalisation.
            now.AddHours(1),
            ResolveDisplayName(principal),
            claims));
    }

    private static string? ResolveSubjectId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("sub")
        ?? principal.Identity?.Name;

    private static string? ResolveDisplayName(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name)
        ?? principal.FindFirstValue("name")
        ?? principal.FindFirstValue(ClaimTypes.Email);
}
