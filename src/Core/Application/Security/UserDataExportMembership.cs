namespace Callora.Core.Application.Security;

/// <summary>One workspace membership in a user data export.</summary>
public sealed record UserDataExportMembership(
    string WorkspaceKey,
    string Role,
    DateTimeOffset AssignedAtUtc);
