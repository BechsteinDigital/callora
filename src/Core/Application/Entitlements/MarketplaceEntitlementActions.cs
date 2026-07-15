namespace Callora.Core.Application.Entitlements;

/// <summary>
/// Supported marketplace entitlement event actions.
/// </summary>
public static class MarketplaceEntitlementActions
{
    public const string Grant = "grant";
    public const string Revoke = "revoke";

    public static bool IsSupported(string? action) =>
        string.Equals(action, Grant, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(action, Revoke, StringComparison.OrdinalIgnoreCase);
}
