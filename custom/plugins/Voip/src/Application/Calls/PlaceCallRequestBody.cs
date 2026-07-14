namespace Callora.Plugins.Voip.Application.Calls;

/// <summary>Body of POST /api/calls.</summary>
public sealed record PlaceCallRequestBody(
    string Target,
    string? TargetDisplayName = null,
    string? ChannelId = null);
