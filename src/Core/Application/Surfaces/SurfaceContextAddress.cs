namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Who a published context value is for.
/// <para>
/// Every field narrows: a value addressed to a workspace reaches every surface in it, one that
/// also names a surface reaches only that surface, one that also names a subject reaches only
/// that visitor's connections. There is no wildcard the other way — a publisher cannot address
/// a subject without naming the workspace they belong to.
/// </para>
/// <para>
/// <see cref="SubjectId"/> is optional but rarely absent by accident. An active call belongs to
/// the agent handling it, not to everyone with the surface open; leaving it null publishes to
/// every visitor of that surface, which is right for a queue length and wrong for a customer
/// record. The publisher decides, and the decision is visible at the call site.
/// </para>
/// </summary>
/// <param name="WorkspaceKey">The workspace whose surfaces may receive this value.</param>
/// <param name="SurfaceKey">One surface within it, or null for all of them.</param>
/// <param name="Issuer">Identity provider of <paramref name="SubjectId"/>; required with it.</param>
/// <param name="SubjectId">One visitor, or null for every visitor of the addressed surfaces.</param>
public sealed record SurfaceContextAddress(
    string WorkspaceKey,
    string? SurfaceKey = null,
    string? Issuer = null,
    string? SubjectId = null)
{
    /// <summary>
    /// Whether this address covers a connection. A subject-scoped address needs BOTH parts to
    /// match: a subject id alone is not an identity — the same id from a different issuer is a
    /// different person (ADR-017).
    /// </summary>
    public bool Covers(string workspaceKey, string surfaceKey, string? issuer, string? subjectId)
    {
        if (!string.Equals(WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (SurfaceKey is not null &&
            !string.Equals(SurfaceKey, surfaceKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (SubjectId is null)
        {
            return true;
        }

        return string.Equals(Issuer, issuer, StringComparison.Ordinal) &&
               string.Equals(SubjectId, subjectId, StringComparison.Ordinal);
    }
}
