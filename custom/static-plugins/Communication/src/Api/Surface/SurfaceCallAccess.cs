using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// Who, on a surface, may see and operate the workspace's telephony.
/// </summary>
/// <remarks>
/// <para>Authentication alone is deliberately <b>not</b> enough here, unlike for most surface routes.
/// The workspace's calls are not the caller's own data: <c>GET calls</c> answers with every call the
/// tenant made and received. On a workplace surface that is exactly right, and on a customer portal —
/// which authenticates customers just as truthfully — it would hand a visitor the phone records of
/// the business they are a customer of.</para>
/// <para>So the bar is a claim the surface's identity provider issues, and the host transports without
/// interpreting: <see cref="ClaimKey"/> carrying <see cref="Read"/> or <see cref="Manage"/>. The split
/// mirrors the operator permissions, because it is the same distinction — a wallboard that displays
/// calls has no business hanging one up.</para>
/// <para>Fails closed and says why: a refusal names the claim it wanted, or a deployment that issues
/// the wrong one debugs an empty panel instead of a message.</para>
/// </remarks>
public static class SurfaceCallAccess
{
    /// <summary>Claim the surface's identity provider issues to people who work with calls.</summary>
    public const string ClaimKey = "communication.calls";

    /// <summary>Claim value for seeing calls and history.</summary>
    public const string Read = "read";

    /// <summary>Claim value for acting on calls — answering, ending, dialling.</summary>
    public const string Manage = "manage";

    /// <summary>
    /// Resolves the workspace a surface call route may act in, or the response explaining why it may
    /// not. <paramref name="required"/> is <see cref="Read"/> or <see cref="Manage"/>; a caller
    /// holding <see cref="Manage"/> may also read, because refusing that would be a distinction with
    /// no meaning.
    /// </summary>
    public static bool TryResolve(
        HostSurfaceApiRequest request,
        string required,
        out string workspaceKey,
        out HostSurfaceApiResponse? error)
    {
        ArgumentNullException.ThrowIfNull(request);

        workspaceKey = request.WorkspaceKey?.Trim() ?? string.Empty;
        if (workspaceKey.Length == 0)
        {
            error = new HostSurfaceApiResponse(400, new { error = "This surface has no workspace." });
            return false;
        }

        if (request.Caller is not AuthenticatedSurfaceCaller authenticated)
        {
            error = new HostSurfaceApiResponse(401, new { error = "Sign in to use the phone." });
            return false;
        }

        if (!Grants(authenticated, required))
        {
            error = new HostSurfaceApiResponse(403, new
            {
                error = $"This account is missing the '{ClaimKey}' claim with value '{required}'.",
            });
            return false;
        }

        error = null;
        return true;
    }

    private static bool Grants(AuthenticatedSurfaceCaller caller, string required)
    {
        if (!caller.Identity.Claims.TryGetValue(ClaimKey, out var values))
        {
            return false;
        }

        return values.Contains(required, StringComparer.OrdinalIgnoreCase)
            || values.Contains(Manage, StringComparer.OrdinalIgnoreCase);
    }
}
