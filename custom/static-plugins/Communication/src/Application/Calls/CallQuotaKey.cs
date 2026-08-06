namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Identifies one quota: an origin's share of one account's lines, inside one workspace.
/// </summary>
/// <param name="WorkspaceKey">Owning workspace — a trunk's lines never cross that boundary.</param>
/// <param name="AccountId">The account whose lines are being divided.</param>
/// <param name="Origin">
/// What is claiming the line, named by the caller — <c>crm</c>, <c>dialer:campaign-x</c>. Declared
/// rather than derived: plugins run trusted in-process (ADR-013), so a quota is an operating limit,
/// not a security boundary, and a plugin misnaming its own origin only misleads itself.
/// </param>
public readonly record struct CallQuotaKey(string WorkspaceKey, string AccountId, string Origin)
{
    /// <summary>Whether this quota belongs to the given account — used when replacing its configuration.</summary>
    public bool Matches(string workspaceKey, string accountId) =>
        string.Equals(WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(AccountId, accountId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Case-insensitive on the operator-entered keys, ordinal on the origin — that one is matched
    /// against what a plugin passes, and a case-folding surprise there would be hard to see.
    /// </summary>
    public bool Equals(CallQuotaKey other) =>
        Matches(other.WorkspaceKey, other.AccountId) &&
        string.Equals(Origin, other.Origin, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(
        WorkspaceKey.GetHashCode(StringComparison.OrdinalIgnoreCase),
        AccountId.GetHashCode(StringComparison.OrdinalIgnoreCase),
        Origin.GetHashCode(StringComparison.Ordinal));
}
