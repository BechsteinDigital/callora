namespace Callora.Core.Application.Security.Events;

/// <summary>
/// Stable business-event names for global user-account lifecycle changes. Consumers
/// (flows, webhooks, plugin listeners) subscribe by these dotted names.
/// </summary>
public static class UserEventTypes
{
    /// <summary>A new user account was created.</summary>
    public const string Created = "user.created";

    /// <summary>An existing user account was updated.</summary>
    public const string Updated = "user.updated";

    /// <summary>A user account was deleted (and its audit trail anonymized).</summary>
    public const string Deleted = "user.deleted";
}
