namespace Callora.Administration.Api;

/// <summary>One feature flag and its state (PLAT-263).</summary>
public sealed record FeatureFlagApiResponse(string Key, bool Enabled);
