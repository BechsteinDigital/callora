using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Security.Events;

/// <summary>
/// Describes the global user-account lifecycle business events for discovery
/// (flow-builder, webhook UI): which events the host publishes and which fields they carry.
/// </summary>
public sealed class UserBusinessEventProvider : IBusinessEventProvider
{
    private static readonly IReadOnlyList<BusinessEventField> IdentityFields =
    [
        new("userId", BusinessEventFieldType.Text, "User"),
        new("email", BusinessEventFieldType.Text, "Email"),
        new("displayName", BusinessEventFieldType.Text, "Display name"),
    ];

    private static readonly IReadOnlyList<BusinessEventField> DeletedFields =
    [
        new("userId", BusinessEventFieldType.Text, "User"),
    ];

    /// <inheritdoc />
    public IReadOnlyList<BusinessEventDescriptor> GetDescriptors() =>
    [
        new(UserEventTypes.Created, "User created", IdentityFields),
        new(UserEventTypes.Updated, "User updated", IdentityFields),
        new(UserEventTypes.Deleted, "User deleted", DeletedFields),
    ];
}
