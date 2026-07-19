namespace Callora.Core.Application.Media.Events;

/// <summary>
/// Stable business-event names for workspace media asset lifecycle changes.
/// Consumers (flows, webhooks, plugin listeners) subscribe by these dotted names.
/// </summary>
public static class MediaEventTypes
{
    /// <summary>A media asset was uploaded to a workspace.</summary>
    public const string Uploaded = "media.uploaded";

    /// <summary>A media asset was deleted from a workspace.</summary>
    public const string Deleted = "media.deleted";
}
