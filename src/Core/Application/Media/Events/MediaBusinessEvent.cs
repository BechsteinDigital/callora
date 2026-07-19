using System.Globalization;
using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Media.Events;

/// <summary>
/// A workspace media asset business event (uploaded/deleted), published to the
/// business-event bus so flows, webhooks and plugin listeners react to media changes —
/// e.g. transcoding, virus-scanning or CDN sync (PLAT-270).
/// </summary>
public sealed class MediaBusinessEvent : IBusinessEvent
{
    private readonly MediaItemSnapshot _item;

    private MediaBusinessEvent(string eventName, MediaItemSnapshot item)
    {
        EventName = eventName;
        _item = item;
    }

    /// <inheritdoc />
    public string EventName { get; }

    /// <inheritdoc />
    public string? WorkspaceKey => _item.WorkspaceKey;

    /// <summary>Builds an uploaded event for a stored media asset.</summary>
    public static MediaBusinessEvent Uploaded(MediaItemSnapshot item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new MediaBusinessEvent(MediaEventTypes.Uploaded, item);
    }

    /// <summary>Builds a deleted event for a removed media asset.</summary>
    public static MediaBusinessEvent Deleted(MediaItemSnapshot item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new MediaBusinessEvent(MediaEventTypes.Deleted, item);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToEventData() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["mediaId"] = _item.Id.ToString(),
        ["workspaceKey"] = _item.WorkspaceKey,
        ["fileName"] = _item.FileName,
        ["contentType"] = _item.ContentType,
        ["folder"] = _item.Folder,
        ["sizeBytes"] = _item.SizeBytes.ToString(CultureInfo.InvariantCulture),
    };
}
