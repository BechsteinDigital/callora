using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Media.Events;

/// <summary>
/// Describes the workspace media asset business events for discovery (flow-builder,
/// webhook UI): which events the host publishes and which fields they carry.
/// </summary>
public sealed class MediaBusinessEventProvider : IBusinessEventProvider
{
    private static readonly IReadOnlyList<BusinessEventField> MediaFields =
    [
        new("mediaId", BusinessEventFieldType.Text, "Media ID"),
        new("workspaceKey", BusinessEventFieldType.Text, "Workspace"),
        new("fileName", BusinessEventFieldType.Text, "File name"),
        new("contentType", BusinessEventFieldType.Text, "Content type"),
        new("folder", BusinessEventFieldType.Text, "Folder"),
        new("sizeBytes", BusinessEventFieldType.Number, "Size (bytes)"),
    ];

    /// <inheritdoc />
    public IReadOnlyList<BusinessEventDescriptor> GetDescriptors() =>
    [
        new(MediaEventTypes.Uploaded, "Media uploaded", MediaFields),
        new(MediaEventTypes.Deleted, "Media deleted", MediaFields),
    ];
}
