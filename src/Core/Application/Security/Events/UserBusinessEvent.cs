using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Security.Events;

/// <summary>
/// A global user-account business event (created/updated/deleted), published to the
/// business-event bus so flows, webhooks and plugin listeners react to account
/// provisioning and deprovisioning (PLAT-270). Users are platform-wide, so these
/// events carry no workspace scope.
/// </summary>
public sealed class UserBusinessEvent : IBusinessEvent
{
    private readonly IReadOnlyDictionary<string, string> _data;

    private UserBusinessEvent(string eventName, IReadOnlyDictionary<string, string> data)
    {
        EventName = eventName;
        _data = data;
    }

    /// <inheritdoc />
    public string EventName { get; }

    /// <inheritdoc />
    public string? WorkspaceKey => null;

    /// <summary>Builds a created event from the account identity.</summary>
    public static UserBusinessEvent Created(string externalId, string? email, string? displayName) =>
        WithIdentity(UserEventTypes.Created, externalId, email, displayName);

    /// <summary>Builds an updated event from the account identity.</summary>
    public static UserBusinessEvent Updated(string externalId, string? email, string? displayName) =>
        WithIdentity(UserEventTypes.Updated, externalId, email, displayName);

    /// <summary>Builds a deleted event carrying only the account id.</summary>
    public static UserBusinessEvent Deleted(string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        return new UserBusinessEvent(
            UserEventTypes.Deleted,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["userId"] = externalId,
            });
    }

    private static UserBusinessEvent WithIdentity(string eventName, string externalId, string? email, string? displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        return new UserBusinessEvent(
            eventName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["userId"] = externalId,
                ["email"] = email ?? string.Empty,
                ["displayName"] = displayName ?? string.Empty,
            });
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToEventData() => _data;
}
