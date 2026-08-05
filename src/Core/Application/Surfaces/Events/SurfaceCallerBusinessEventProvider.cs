using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Surfaces.Events;

/// <summary>
/// Describes the surface caller business events for discovery (flow-builder,
/// webhook UI): which events the host publishes and which fields they carry.
/// </summary>
public sealed class SurfaceCallerBusinessEventProvider : IBusinessEventProvider
{
    private static readonly IReadOnlyList<BusinessEventField> PromotionFields =
    [
        new("workspaceKey", BusinessEventFieldType.Text, "Workspace"),
        new("surfaceKey", BusinessEventFieldType.Text, "Surface"),
        new("previousIssuer", BusinessEventFieldType.Text, "Previous issuer"),
        new("previousSubjectId", BusinessEventFieldType.Text, "Previous subject"),
        new("issuer", BusinessEventFieldType.Text, "Issuer"),
        new("subjectId", BusinessEventFieldType.Text, "Subject"),
    ];

    /// <inheritdoc />
    public IReadOnlyList<BusinessEventDescriptor> GetDescriptors() =>
    [
        new(SurfaceCallerEventTypes.Promoted, "Surface guest promoted to an identity", PromotionFields),
    ];
}
