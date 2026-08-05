using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Surfaces.Events;

/// <summary>
/// Published when a guest context turns into an authenticated one (ADR-017 §8.3).
/// The context token rotates at that moment for session-fixation reasons, and the
/// subject changes with it — so anything a plugin keyed on the guest subject needs
/// the old and the new one to move its data across. A plugin that ignores this event
/// loses the visitor's cart at login.
/// </summary>
public sealed class SurfaceCallerBusinessEvent : IBusinessEvent
{
    private readonly IReadOnlyDictionary<string, string> _data;

    private SurfaceCallerBusinessEvent(
        string eventName,
        string workspaceKey,
        IReadOnlyDictionary<string, string> data)
    {
        EventName = eventName;
        WorkspaceKey = workspaceKey;
        _data = data;
    }

    /// <inheritdoc />
    public string EventName { get; }

    /// <inheritdoc />
    public string? WorkspaceKey { get; }

    /// <summary>Builds the promotion event from the previous and the new subject.</summary>
    /// <param name="workspaceKey">Workspace the surface belongs to.</param>
    /// <param name="surfaceKey">Surface the promotion happened on.</param>
    /// <param name="previous">The guest subject that is being left behind.</param>
    /// <param name="current">The authenticated subject taking its place.</param>
    public static SurfaceCallerBusinessEvent Promoted(
        string workspaceKey,
        string surfaceKey,
        SurfaceSubject previous,
        SurfaceSubject current)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceKey);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        return new SurfaceCallerBusinessEvent(
            SurfaceCallerEventTypes.Promoted,
            workspaceKey,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["workspaceKey"] = workspaceKey,
                ["surfaceKey"] = surfaceKey,
                ["previousIssuer"] = previous.Issuer,
                ["previousSubjectId"] = previous.SubjectId,
                ["issuer"] = current.Issuer,
                ["subjectId"] = current.SubjectId,
            });
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToEventData() => _data;
}
